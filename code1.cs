using System.Data.Common;
using HP.ST.Ext.CommunicationChannels.ConcreteChannels.Wse2SecurityChannel;
using HP.ST.Fwk.SOAReplayAPI;

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
	using System.Data;
	using System.Data.OleDb;
	using System.Collections.Generic;
	
	
	[Serializable()]
	public class TestUserCode : TestEntities
	{
		//Global Variables
		OleDbConnection dbConnection = null;
		OleDbCommand command = null;
		OleDbDataAdapter dataAdapter = null;
		DataRowCollection dataRows = null;
		object[] rowObject = null;
		string firstDynamicData = string.Empty;
		string secondDynamicData = string.Empty;
		string customisedSqlQuery = string.Empty;
		string customisedCountSqlQuery = string.Empty;
		string XMLString = string.Empty;
		CheckpointEventArgs checkArgs = null;
		private ProductResponse DeserializedXMLproductResponse {get; set;}
		RecordDTO record = null;
		List<Product2DTO> productList = null;
		Product2DTO product = null;
		
		/// <summary>
		/// Handler for the CodeActivity7 Activity’s ExecuteEvent event.
		/// </summary>

		public void CodeActivity7_OnExecuteEvent(object sender, STActivityBaseEventArgs args)
		{
			// initialize output parameter with default values
			this.CodeActivity7.Output.Out_QParameter = this.CodeActivity7.Input.QParameter;
			this.CodeActivity7.Output.Out_SQLParameter = this.CodeActivity7.Input.SQLQueryToValidateData;
			
			//Check database connection and retrieve reocrds from database.
			OpenDBConnection(this.CodeActivity7.Input.DBConnectionString);
			
			//Verify whether SQLQuery exist
			if(this.CodeActivity7.Input.SQLToFetchData.Length > 0)
			{
				//Fetch the dymanic value for customising the URL from the database
				dataRows = GetRecordsFromDB(this.CodeActivity7.Input.SQLToFetchData, this.CodeActivity7);

				rowObject = dataRows[0].ItemArray;
				//Verify data columns exist before fetching data
				if(rowObject.Length > 0)
				{
					firstDynamicData = rowObject[0].ToString();
					
					//verify whether second data column exist
					if(rowObject.Length > 1)
					{
						secondDynamicData = rowObject[1].ToString();
					}
					
					this.CodeActivity7.Output.Out_QParameter = this.CodeActivity7.Output.Out_QParameter.Replace("XXX", firstDynamicData).Replace("YYY", secondDynamicData);
					this.CodeActivity7.Output.Out_SQLParameter = this.CodeActivity7.Input.SQLQueryToValidateData.Replace("XXX", firstDynamicData).Replace("YYY", secondDynamicData);
				}
			}
			
			//Close DB connection
			CloseDBConnection();
		}
		
		
		/// <summary>
		/// Handler for the RESTActivityV24 Activity’s CodeCheckPointEvent event.
		/// </summary>

		public void RESTActivityV24_OnCodeCheckPointEvent(object sender, CheckpointEventArgs args)
		{
			//checking of 200 pass/fail transaction
			if(this.RESTActivityV24.StatusCode == 200)
			{
				this.RESTActivityV24.Output.Transaction_Status = "PASS";
			}
			else
			{
				this.RESTActivityV24.Output.Transaction_Status = "FAIL";
			}	
		}
		
		/// <summary>
		/// This method is used to fetch record from the database
		/// </summary>
		/// <param name="customizedCountSqlQuery">Customized Count SqlQuery</param>
		/// <param name="customizedSqlQuery">Customized SqlQuery</param>
		/// <returns>RecordDTO object</returns>
		private RecordDTO GetRecord(string customizedCountSqlQuery, string customizedSqlQuery, STActivityBase activity)
		{
			record = new RecordDTO();
			
			//Get the count
			dataRows = GetRecordsFromDB(customisedCountSqlQuery, activity);
			
			int count = Convert.ToInt32(dataRows[0].ItemArray[0]);
			
			record.RecordsCount = count;

			//for the iteration q=*:*  we are assigning null value to record.product
			record.Product = null;
			
			//Get the product data
			record.Product = GetproductRecords(customisedSqlQuery, activity);
			
			return record;
		}
		
		/// <summary>
		/// This method is used to fetch Branchclaim records from the database
		/// </summary>
		/// <param name="customisedSqlQuery">CustomisedSqlQuery</param>
		/// <returns>List<NoteDTO></returns>
		private List<Product2DTO> GetproductRecords(string customisedSqlQuery, STActivityBase activity)
		{
			productList = new List<Product2DTO>();
			product = null;
			
			//Verify whether SQLQuery exist
			if(customisedSqlQuery.Length > 0)
			{
				//fetch the record from the database
				dataRows = GetRecordsFromDB(customisedSqlQuery, activity);
				
				foreach (DataRow dataRow in dataRows)
				{
					rowObject = dataRow.ItemArray;
					//Verify data has been fetched
					if(rowObject != null)
					{
						product = new Product2DTO();
						product.productId = rowObject[0].ToString();
						product.description = rowObject[1].ToString();
						product.discGroupId = rowObject[4].ToString();
						product.discGroupDesc = rowObject[5].ToString();
						product.linebuyId = rowObject[6].ToString();
						product.linebuyDesc = rowObject[7].ToString();
						product.alt1Code = rowObject[8].ToString();
						product.altCodes = rowObject[9].ToString();
						product.altCodeType = rowObject[10].ToString();
						productList.Add(product);
					}

				}
			}
			
			return productList;
		}
		
		/// <summary>
		/// This reusable method is used to check if expected and actual object is not null else perform string comparison
		/// </summary>
		public void AssertIfNotNull(CheckpointEventArgs cehckArgs, object expectedObj, object actualObj, string sDescription)
		{
			if(expectedObj !=null && actualObj != null)
			{
				//verify if values match
				cehckArgs.Checkpoint.Assert.Equals("", expectedObj.ToString(), actualObj.ToString(), sDescription);
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
			if(Convert.ToBoolean(SwitchEnvironment14.IsLoadTestEnabled) == true)
			{
				isIterationStatusAvailable = false;
			}
			else
			{
				// Fetch the value of iteration numbers
				Loop2.NumberOfIterations = GetDataSource("Product!MTC006").RowsCount;
				if(Loop2.CurrentIterationNumber > Loop2.NumberOfIterations - 1)
				{
					isIterationStatusAvailable = false;
				}
			}
			
			return isIterationStatusAvailable;
		}
		
		//custom function to retrive the records from database also validte the jenkins code
		public DataRowCollection GetRecordsFromDB(string sqlQuery,STActivityBase activity)
		{
			DataRowCollection dbRows = null;
			string environment= this.SwitchEnvironment14.pEnvironment.ToString();
			
			//jenkins code check (if env values are coming from jenkins)
			switch (environment.ToLower())
			{
				case "Development_Trunk":
					sqlQuery=sqlQuery.ToLower().Replace("serv_user.","").ToString();
					sqlQuery=sqlQuery.ToLower().Replace("ods_manager.","").ToString();
					break;
					
				case "Development_Branch":
					sqlQuery=sqlQuery.ToLower().Replace("serv_user.","").ToString();
					sqlQuery=sqlQuery.ToLower().Replace("ods_manager.","").ToString();
					break;
			}

			//get the records from DB
			dbRows = GetRecords.GetRecordsFromDatabase(dbConnection, command, dataAdapter, sqlQuery);
			
			//checking of DB row count
			if (dbRows.Count == 0)
			{
				activity.Report("DB records returned : " + dbRows.Count.ToString(),"Zero Data rows returned");
				string s= null;
				s.ToLower();
			}
			
			return dbRows;
		}
		
		
		/// <summary>
		/// This method is used to open up the connection
		/// </summary>
		private void OpenDBConnection(string connectionString)
		{
			if(dbConnection == null)
			{
				//Create the object of the OLEDB connection
				dbConnection = new System.Data.OleDb.OleDbConnection(connectionString);
				//Open the OLEDB connection
				dbConnection.Open();
			}
		}
		
		
		/// <summary>
		/// Close the database connection
		/// </summary>
		private void CloseDBConnection()
		{
			if(dbConnection.State == ConnectionState.Open)
			{
				dbConnection.Close();
				dbConnection = null;
			}
		}

		/// <summary>
		/// Handler for the CodeActivity17 Activity’s ExecuteEvent event.
		/// </summary>
		/// <param name=\"sender\">The activity object that raised the ExecuteEvent event.</param>
		/// <param name=\"args\">The event arguments passed to the activity.</param>
		/// Use this.CodeActivity17 to access the CodeActivity17 Activity's context, including input and output properties.
		public void CodeActivity17_OnExecuteEvent(object sender, STActivityBaseEventArgs args)
		{
			if(SwitchEnvironment14.IsLoadTestEnabled != "true")
			{
				int docArrayElementCount = 0;
				checkArgs = new CheckpointEventArgs(this.CodeActivity17);
				
				//Get the response
				XMLString = this.RESTActivityV24.ResponseBody;
				
				//Create Deserialize object
				DeserializedXMLproductResponse = XMLString.DeserializeFromXMLInput<ProductResponse>();
				
				//Customize the SQL Query for count
				customisedCountSqlQuery = "Select count(*) from (" + this.CodeActivity7.Output.Out_SQLParameter + ")";
				
				//Customize the SQL Query
				customisedSqlQuery = this.CodeActivity7.Output.Out_SQLParameter;
				
				//opening the DB conenction
				OpenDBConnection(this.CodeActivity7.Input.DBConnectionString);
				
				//Get the record from the database
				record = GetRecord(customisedCountSqlQuery, customisedSqlQuery, this.CodeActivity17);
				
				//verify database records count
				checkArgs.Checkpoint.Assert.Equals("", DeserializedXMLproductResponse.RowsTotal.ToString(), record.RecordsCount.ToString(), "Records count");
				
				//Verify product data
				foreach(Product doc_ProductNote in DeserializedXMLproductResponse.Products.Product)
				{
					//isRecordAvailable = false;
					foreach(Product2DTO recordNote in record.Product)
					{
						//Compare product Primary key
						if(doc_ProductNote.ProductId.ToString() == recordNote.productId)
						{
							
							string altCodes = sortString(recordNote.altCodes);
							
							//Verify productId
							AssertIfNotNull(checkArgs, doc_ProductNote.ProductId, recordNote.productId, "Product V2 productId: " + docArrayElementCount);
							//Verify productDesc
							AssertIfNotNull(checkArgs, doc_ProductNote.Description, recordNote.description, "Product V2 prodDesc: " + docArrayElementCount);
							//Verify discGroupId
							AssertIfNotNull(checkArgs, doc_ProductNote.DiscGroupId, recordNote.discGroupId, "Product V2 discGroupId: " + docArrayElementCount);
							//Verify discGroupDesc
							AssertIfNotNull(checkArgs, doc_ProductNote.DiscGroupDesc, recordNote.discGroupDesc, "Product V2 discGroupDesc: " + docArrayElementCount);
							//Verify linbuyId
							AssertIfNotNull(checkArgs, doc_ProductNote.LinebuyId, recordNote.linebuyId, "Product V2 linebuyId: " + docArrayElementCount);
							//Verify linebuyDesc
							AssertIfNotNull(checkArgs, doc_ProductNote.LinebuyDesc, recordNote.linebuyDesc, "Product V2 linebuyDesc: " + docArrayElementCount);
							//Verify altCode1
							AssertIfNotNull(checkArgs, doc_ProductNote.Alt1Code, recordNote.alt1Code, "Product V2 altCode1: " + docArrayElementCount);
							//Verify altCodes
							AssertIfNotNull(checkArgs, sortString(doc_ProductNote.AltCodes.Trim()), sortString(recordNote.altCodes), "Product V2 altCodes: " + docArrayElementCount);
							//Verify altCodeType
							AssertIfNotNull(checkArgs, sortString(doc_ProductNote.AltCodeType.Trim()), sortString(recordNote.altCodeType), "Product V2 altCodeType: " + docArrayElementCount);
							
							break;
						}
					}
					docArrayElementCount++;
				}
				
				//closing the DB connection
				CloseDBConnection();
			}
		}
		
		//custom function to sort a list of string
		public string sortString(string inputString)
		{
			string outputString = string.Empty;
			
			//splitting the string into array by removing extra sapces
			string[] stringValues = inputString.Replace(" ", "").Split(',');
			
			//sorting the array
			Array.Sort(stringValues);
			
			//adding the array elements to string
			foreach(string value in stringValues)
			{
				outputString = outputString + value;
				outputString = outputString + ", ";
			}
			
			//returning the sorted string
			return outputString.Substring(0, outputString.Length - 2);
		}
		
	}
}




