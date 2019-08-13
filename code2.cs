namespace Script
{
	using System;
	using System.Xml;
	using System.Xml.Schema;
	using HP.ST.Ext.BasicActivities;
	using HP.ST.Fwk.RunTimeFWK;
	using HP.ST.Fwk.RunTimeFWK.ActivityFWK;
	using HP.ST.Fwk.RunTimeFWK.Utilities;
	using HP.ST.Fwk.RunTimeFWK.CompositeActivities;
	using HP.ST.Ext.CustomDataProviders.Extensions;
	using HP.ST.Ext.CustomDataProviders.ExcelFileArguments;
	using System.Collections.Generic;
	using System.Data;
	using System.Data.OleDb;
	using Script;
	using System.IO;
	using System.Text.RegularExpressions;
	
	[Serializable()]
	public class TestUserCode : TestEntities
	{
		#region Global Variables
		//Database Variables
		OleDbConnection connection = null;
		DataRowCollection datarows = null;
		DataRowCollection datarowsMaster = null;
		DataRowCollection datarowsBranch = null;
		OleDbCommand command = null;
		OleDbDataAdapter dataAdapter=null;
		string connectionString = string.Empty;
		SalesOrderDTO salesOrder=null;
		List<SalesOrderDTO> salesOrderList=null;
		object[] rowObject=null;
		
		//Jenkins datasource name
		string jenkinsDataSource = "Jenkins";
		
		//checkArgsPOINT class object
		CheckpointEventArgs checkArgs = null;
		
		//JSON Deserialization object
		string XMLResponse = string.Empty;
		//Create object of response class
		private SearchSalesOrdersResponse DserialisedXMLResponse {get;set;}
		
		
		
		#endregion
		
		//VerificationAssert function
		private void VerificationAssert(object actual, object expected, string description)
		{
			//Verify value is null if it's null then skip the assertion
			if (actual != null && expected != null)
			{
				checkArgs.Checkpoint.Assert.Equals("", actual.ToString(), expected.ToString(), description);
			}
		}
		
		/// <summary>
		/// This method is used to open up the connection
		private void OpenDBConnection(string connectionString)
		{
			if(connection == null)
			{
				//Create the object of the OLEDB connection
				connection = new System.Data.OleDb.OleDbConnection(connectionString);
				//Open the OLEDB connection
				connection.Open();
			}
		}
		
		/// <summary>
		/// Close the database connection
		/// </summary>
		private void CloseDBConnection()
		{
			if(connection.State == ConnectionState.Open)
			{
				connection.Close();
				connection = null;
			}
			
		}
		
		//To check DB row count
		public void CheckDataRowsCount(int dbcount, STActivityBase activity)
		{
			if (dbcount == 0)
			{
				activity.Report("DB records returned : " + dbcount.ToString(),"Zero Data rows returned");
				string s= null;
				s.ToLower();
			}
		}

		/// <summary>
		/// Handles modifying database user name for different
		/// environments
		/// </summary>
		/// <param name="environment">pEnvironment Name </param>
		/// <param name="sqlQuery">Sql query </param>
		/// <param name="dataSource">Data Source Name for Jenkins data</param>
		/// <returns></returns>
		public string jenkinsHandle (string environment,string sqlQuery, string dataSource)
		{
			
			//This only executes in Jenkins "Switchyard" environments
			if(environment.ToLower().Trim().Contains("switchyard"))
			{
				int rowCount = GetDataSource(dataSource).RowsCount;
				for(int i = 0; i < rowCount ; i++)
				{
					string regressionValue = GetDataSource(dataSource).GetValue(i,"oldValue").ToString();
					string jenkinsValue = GetDataSource(dataSource).GetValue(i,"newValue").ToString();
					sqlQuery = Regex.Replace(sqlQuery, regressionValue, jenkinsValue, RegexOptions.IgnoreCase);
				}
			}
			
			return sqlQuery;
		}
		
		/// <summary>
		/// Handler for the CodeActivity21 Activity’s ExecuteEvent event.
		/// </summary>
		/// <param name=\"sender\">The activity object that raised the ExecuteEvent event.</param>
		/// <param name=\"args\">The event arguments passed to the activity.</param>
		/// Use this.CodeActivity21 to access the CodeActivity21 Activity's context, including input and output properties.
		public void CodeActivity21_OnExecuteEvent(object sender, STActivityBaseEventArgs args)
		{
			//Initialize checkArgsPoint object
			checkArgs = new CheckpointEventArgs(this.CodeActivity21);
			//open Database connection
			OpenDBConnection(this.CodeActivity21.Input.connectionString);
			
			//Get the record from the database
			datarowsMaster = GetRecords.GetRecordsFromDatabase(connection,command,dataAdapter,jenkinsHandle(this.SwitchEnvironment27.pEnvironment,this.CodeActivity21.Input.sqlQueryMaster,jenkinsDataSource));
			
			//check record count
			CheckDataRowsCount(datarowsMaster.Count, this.CodeActivity21);
			
			// Get the Master customer Id
			this.CodeActivity21.Output.Mstr_Cust_Id=datarowsMaster[0].ItemArray[0].ToString();
			
			//Replace placeholders in request body
			string requestBody = this.CodeActivity21.Input.requestBody.Replace("Master_Cust_Id_value",this.CodeActivity21.Output.Mstr_Cust_Id).ToString();
			
			if(this.CodeActivity21.Input.caseType.ToUpper() == "MASTERANDBRANCH")
			{
				datarowsBranch = GetRecords.GetRecordsFromDatabase(connection,command,dataAdapter,jenkinsHandle(this.SwitchEnvironment27.pEnvironment,
				                                                                                                this.CodeActivity21.Input.sqlQueryBranch.Replace("mstr_cust_id_value", datarowsMaster[0].ItemArray[0].ToString()),jenkinsDataSource));
				
				CheckDataRowsCount(datarowsBranch.Count, this.CodeActivity21);
				
				//Replace placeholders in request body
				requestBody = requestBody.Replace("Branch_Cust_Id_value", datarowsBranch[0].ItemArray[0].ToString().Split('*')[1]).
					Replace("Branch_Cust_Acct", datarowsBranch[0].ItemArray[0].ToString().Split('*')[0]);
			}
			
			this.CodeActivity21.Output.requestBodyOut = requestBody;
			
			//Close database connection
			CloseDBConnection();
		}
		
		public List<SalesOrderDTO> GetListOfAllSalesOrders(string SqlQuery)
		{
			
			salesOrderList=new List<SalesOrderDTO>();
			//Get  the records from database
			datarows=GetRecords.GetRecordsFromDatabase(connection,command,dataAdapter,jenkinsHandle(this.SwitchEnvironment27.pEnvironment,SqlQuery,jenkinsDataSource));
			foreach(DataRow dataRow in datarows)
			{
				salesOrder=new SalesOrderDTO();
				rowObject=dataRow.ItemArray;
				salesOrder.sale_key=rowObject[0].ToString();
				salesOrder.Cust_Key=rowObject[1].ToString();
				salesOrder.order_Date=rowObject[2].ToString();
				salesOrder.Order_Code=rowObject[3].ToString();
				salesOrder.sale_Acct=rowObject[4].ToString();
				salesOrder.sale_Id=rowObject[5].ToString();
				salesOrder.job_Name=rowObject[6].ToString();
				salesOrder.bmi_bud_cust_type=rowObject[7].ToString();
				salesOrder.total_amount=rowObject[8].ToString();
				salesOrder.Bid_Expire_Date=rowObject[9].ToString();
				salesOrder.srcSysCode=rowObject[10].ToString();
				salesOrder.custPoNum=rowObject[11].ToString();
				salesOrder.created_By=rowObject[12].ToString();
				salesOrder.Company_Name=rowObject[13].ToString();
				salesOrder.user_Email=rowObject[14].ToString();
				salesOrderList.Add(salesOrder);
				
			}
			
			return salesOrderList;
		}
		
		

		/// <summary>
		/// Handler for the RESTActivityV220 Activity’s CodeCheckPointEvent event.
		/// </summary>
		/// <param name=\"sender\">The activity object that raised the CodeCheckPointEvent event.</param>
		/// <param name=\"args\">The event arguments passed to the activity.</param>
		/// Use this.RESTActivityV220 to access the RESTActivityV220 Activity's context, including input and output properties.
		public void RESTActivityV220_OnCodeCheckPointEvent(object sender, CheckpointEventArgs args)
		{
			if(this.SwitchEnvironment27.IsLoadTestEnabled!= "true")
			{
				// checkArgsPoint
				checkArgs= new CheckpointEventArgs(this.RESTActivityV220);
				//Open database connection
				OpenDBConnection(this.CodeActivity21.Input.connectionString);
				
				//Get all branch customers related to the given master customer id
				salesOrderList=GetListOfAllSalesOrders(this.CodeActivity21.Input.ValidationQuery.Replace("MSTR_CUST_ID_VALUE",this.CodeActivity21.Output.Mstr_Cust_Id));
				
				//close database connection
				CloseDBConnection();
				// Fetch the JSON response
				XMLResponse = this.RESTActivityV220.ResponseBody;
				
				//Create deserialized XML objects
				DserialisedXMLResponse = XMLResponse.DeserializeFromXMLInput<SearchSalesOrdersResponse>();
				
				//Validating the total count of returned records
				VerificationAssert(DserialisedXMLResponse.TotalRecordsFound, salesOrderList.Count,"Checking of total record found");
				
				int recordCountCheck = 0;
				
				foreach(SalesOrder SalesOrderElement  in DserialisedXMLResponse.SalesOrder)
				{
					foreach(SalesOrderDTO record in salesOrderList)
					{
						if(SalesOrderElement.OrderId ==record.sale_Id && SalesOrderElement.OrderAcctId==record.sale_Acct)
						{
							// checkArgs the respective fields for verification
							VerificationAssert(SalesOrderElement.OrderId,record.sale_Id,"Order ID");
							VerificationAssert(SalesOrderElement.OrderAcctId,record.sale_Acct,"Order Account ID");
							VerificationAssert(SalesOrderElement.CustAcctId+"*"+SalesOrderElement.CustId,record.Cust_Key,"Cust Key");
							VerificationAssert(Convert.ToDateTime(SalesOrderElement.OrderDate).ToString("dd/MM/yyyy"),Convert.ToDateTime(record.order_Date).ToString("dd/MM/yyyy"),"Order Date");
							VerificationAssert(SalesOrderElement.JobName,record.job_Name,"Job Name");
							if(record.total_amount != "")
							{
								VerificationAssert(Convert.ToDecimal(SalesOrderElement.TotalAmt).ToString("0.00"),Convert.ToDecimal(record.total_amount).ToString("0.00"),"Total amount");
							}
							VerificationAssert(SalesOrderElement.CustomerPO,record.custPoNum," Customer PO number");
							VerificationAssert(SalesOrderElement.SubmitSiteId,record.srcSysCode,"Submit Site Id");
							VerificationAssert(SalesOrderElement.CreatedBy,record.created_By,"createdBy");
							VerificationAssert(SalesOrderElement.CompanyName,record.Company_Name,"companyName");
							VerificationAssert(SalesOrderElement.UserEmail,record.user_Email,"userEmail");
							
							break;
						}
						
					}
					
					if(recordCountCheck == 10)
					{
						break;
					}
					
					recordCountCheck++;
				}
			}
			else
			{
				this.RESTActivityV220.Output.TransactionStatus= this.RESTActivityV220.StatusCode == 200 ? "PASS" : "FAIL";
			}
		}

		/// <summary>
		/// Handler for the Loop2 Activity’s Condition event.
		/// </summary>
		/// <param name=\"sender\">The activity object that raised the Condition event.</param>
		/// <param name=\"args\">The event arguments passed to the activity.</param>
		/// Use this.Loop2 to access the Loop2 Activity's context, including input and output properties.
		public bool Loop2_OnCondition(object sender, STActivityBaseEventArgs args)
		{
			bool isIterationStatusAvailable = true;
			// Assign iteration status depending on the load testing status
			if(Convert.ToBoolean(SwitchEnvironment27.IsLoadTestEnabled) == true)
			{
				isIterationStatusAvailable = false;
			}
			else
			{
				// Fetch the value of iteration numbers
				Loop2.NumberOfIterations = GetDataSource("MTC029").RowsCount;
				if(Loop2.CurrentIterationNumber > Loop2.NumberOfIterations - 1)
				{
					isIterationStatusAvailable = false;
				}
			}
			return isIterationStatusAvailable;
		}
	}
}