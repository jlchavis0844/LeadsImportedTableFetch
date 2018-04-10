using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using unirest_net.http;
using System.Data.SqlClient;
using System.Data;

namespace LeadsImportedTableFetch {
    class Program {
        public static PayLoad payload;
        public static string token = "";
        public static StreamWriter log;
        private static string connString = "Data Source=RALIMSQL1;Initial Catalog=CAMSRALFG;Integrated Security=SSPI;";
        public static bool stop = false;
        public static List<Lead> leadsList;

        static void Main(string[] args) {
            string startingURL = @"https://api.getbase.com/v3/leads/search";//search API URL
            DateTime now = DateTime.Now;
            string logPath = @"\\NAS3\NOE_Docs$\RALIM\Logs\Base\LeadsCreatedFetch_" + now.ToString("ddMMyyyy") + ".txt"; //log location
            leadsList = new List<Lead>();

            if (!File.Exists(logPath)) {//create log if one does not exist
                using (StreamWriter sw = File.CreateText(logPath)) {
                    sw.WriteLine("Creating Leads Imported log " + now.ToString("ddMMyyyy") + " at " + now.ToString());
                }
            }

            log = File.AppendText(logPath); //open the log
            log.WriteLine("\n\nStarting ECD Fetch at " + now);
            Console.WriteLine("Starting ECD Fetch at " + now);
            log.Flush();

            //load API token
            var fs = new FileStream(@"C:\apps\NiceOffice\token", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            using (var sr = new StreamReader(fs)) {
                token = sr.ReadToEnd();
            }

            payload = new PayLoad(); // builds the body of the POST call
            log.WriteLine("From " + DateTime.Now.Date);
            Console.WriteLine("From " + DateTime.Now.Date);
            string payloadStr = payload.ToString();
            Console.WriteLine(payloadStr);
            string rawJSON = Post(startingURL, token, payload.ToString()); //get the first page
            var jObj = JObject.Parse(rawJSON) as JObject;
            var jArr = jObj["items"][0]["items"];

            int resCnt = -1;//tracks how many leads are new

            try {
                resCnt = Convert.ToInt32(jObj["items"][0]["meta"]["total_count"]); //try to load new lead count
            } catch (Exception ex) {
                Console.WriteLine("Could not get count of results.");
                log.WriteLine("Could not get count of results.");
                log.WriteLine(ex.Message);
                Environment.Exit(-1); // if we can't get an initital count, something is very wrong
            }

            Console.WriteLine("found " + resCnt + " leads");
            log.WriteLine("found " + resCnt + " leads");

            while (!stop) {//stop should be false to continue the loop.
                stop = !(jObj["items"][0]["meta"]["links"].HasValues &&
                    jObj["items"][0]["meta"]["links"]["next_page"] != null);

                foreach (var item in jArr) {
                    int bID = Convert.ToInt32(item["data"]["id"]); //every lead will have a base id
                    int cID = 0;

                    //not every lead will have a client id. Rather than exclude leads, set the client ID to 0 and move on
                    if (item["data"]["custom_fields"]["332106"] == null || item["data"]["custom_fields"]["332106"].HasValues == false) {
                        Console.WriteLine("Found empty Client ID for lead " + bID + ", setting to zero");
                        log.WriteLine("Found empty Client ID for lead " + bID + ", setting to zero");
                    } else {//load client id if it exists
                        cID = Convert.ToInt32(item["data"]["custom_fields"]["332106"]);
                    }

                    //every lead should have a created date
                    DateTime cDate = DateTime.UtcNow.Date;
                    if (item["data"]["created_at"] == null) {
                        Console.WriteLine("Found empty created_at for lead " + bID + ", setting to start of day");
                        log.WriteLine("Found empty Client ID for lead " + bID + ", setting to start of day");
                    } else {
                        cDate = Convert.ToDateTime(item["data"]["created_at"]).ToLocalTime();//load created date
                    }

                    leadsList.Add(new Lead(bID, cID, cDate)); //add the lead with the current info as a Lead object
                }

                if (!stop) {//load data for the next page if not marked to stop
                    string tCurr = jObj["items"][0]["meta"]["links"]["next_page"].ToString();
                    payload.SetCursor(tCurr); //add a cursor to the payload (POST body)
                    rawJSON = Post(startingURL, token, payload.ToString());
                    jObj = JObject.Parse(rawJSON) as JObject;
                    jArr = jObj["items"][0]["items"];
                }
            }

            Console.WriteLine("Loaded " + leadsList.Count + " leads");
            log.WriteLine("Loaded " + leadsList.Count + " leads");

            foreach (var lead in leadsList) { //print all the lead info to console and the log file
                Console.WriteLine(lead.BaseID + "\t" + lead.ClientID + "\t" + lead.created_at);
                log.WriteLine(lead.BaseID + "\t" + lead.ClientID + "\t" + lead.created_at);
            }

            //start the process to delete the existing rows in [CAMSRALFG].[dbo].[LeadsImportedToday]
            string sqlStr = "DELETE FROM [CAMSRALFG].[dbo].[LeadsImportedToday] WHERE [ID] is not null";
            using (SqlConnection connection = new SqlConnection(connString)) {
                SqlCommand delCommand = new SqlCommand(sqlStr, connection);
                try {
                    connection.Open();

                    int result = delCommand.ExecuteNonQuery(); //execute the command and check for 1

                    if (result == 0) { // nothing was deleted
                        log.WriteLine("No entries deleted, previous count was 0?");
                        log.Flush();
                        Console.WriteLine("No entries deleted, previous count was 0?");
                    }
                } catch (Exception ex) {
                    log.WriteLine(ex);
                    log.Flush();
                    Console.WriteLine(ex);
                } finally {
                    connection.Close();
                }
            }

            //write the new leads to [CAMSRALFG].[dbo].[LeadsImportedToday]
            sqlStr = "INSERT INTO [CAMSRALFG].[dbo].[LeadsImportedToday] ([BaseID], [ClientID], [created_at]) VALUES (@bID, @cID, @cDate)";
            using (SqlConnection connection = new SqlConnection(connString)) {
                foreach (var lead in leadsList) {
                    using (SqlCommand command = new SqlCommand(sqlStr, connection)) {
                        command.Parameters.Add("@bID", SqlDbType.Int).Value = lead.BaseID; // Base ID
                        command.Parameters.Add("@cID", SqlDbType.Int).Value = lead.ClientID; // Client ID
                        command.Parameters.Add("@cDate", SqlDbType.DateTime).Value = lead.created_at; // created_at

                        try {
                            connection.Open();

                            int result = command.ExecuteNonQuery(); //execute the call and check affected rows

                            if (result == 0) { //command failed
                                log.WriteLine("INSERT failed for " + command.ToString());
                                log.Flush();
                                Console.WriteLine("INSERT failed for " + command.ToString());
                            }
                        } catch (Exception ex) {
                            log.WriteLine(ex);
                            log.Flush();
                            Console.WriteLine(ex);
                        } finally {
                            connection.Close();
                        }
                    }
                }
            }
            log.WriteLine("Finished at " + DateTime.Now);
            log.Flush(); //make sure the log is written to.
            //Console.WriteLine("any key to continue");
            //Console.ReadLine();
        }//end main

        /// <summary>
        /// Sends the POST call to the given url with the given token using the given payload
        /// </summary>
        /// <param name="url">The URL to make the call to</param>
        /// <param name="token">The API token read from earlier</param>
        /// <param name="payload">The body of the POST call.</param>
        /// <returns></returns>
        public static string Post(string url, string token, string payload) {
            string body = "";
            try {
                HttpResponse<string> jsonReponse = Unirest.post(url)
                    .header("accept", "application/json")
                    .header("Authorization", "Bearer " + token)
                    .body(payload)
                    .asJson<string>();
                body = jsonReponse.Body.ToString();
                return body;
            } catch (Exception ex) {
                log.WriteLine(ex);
                log.Flush();
                Console.WriteLine(ex);
                return body;
            }
        }
    }
}
