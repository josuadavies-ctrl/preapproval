using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using NUnit.Framework;
using MySqlConnector;

namespace OpsAdminComplete
{
    [TestFixture]
    public static class Eligibility
    {
#pragma warning disable NUnit1032
        private static HttpClient? _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };
#pragma warning restore NUnit1032

        private static readonly string EndpointTemplate = "https://rc-loanapi.saldev.net/api/topupapplications/borrower/{0}/eligibility?apiKey=c10cc286-9877-4a3b-a12c-2cff2c7005dc&useDecisionCache=true";
        
        private static readonly string dbConnectionString = "Server=rc-db.saldev.net;Database=Loan;Uid=QAUser;Pwd=Str0ngPa$$123;";

        [OneTimeTearDown]
        public static void Cleanup()
        {
            _httpClient?.Dispose();
            _httpClient = null;
        }

        /// <summary>
        /// Reads the latest email from CSV output, queries database for BorrowerId,
        /// fires the API call, and prints out the exact eligibility code status.
        /// </summary>
        public static async Task<int> TriggerAllAsync()
        {
            _httpClient ??= new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            
            // 1. EXTRACT EMAIL FROM LATEST CSV FILE
            string targetEmail = GetEmailFromLatestCsv();
            if (string.IsNullOrEmpty(targetEmail))
            {
                Console.WriteLine("❌ Script Aborted: No valid email could be parsed from source CSV files.");
                return 0; 
            }

            // 2. QUERY DATABASE USING POPULATED EMAIL
            string borrowerId = await GetBorrowerIdFromDatabaseAsync(targetEmail);
            if (string.IsNullOrEmpty(borrowerId))
            {
                Console.WriteLine($"❌ Script Aborted: No borrower ID found matching the email token '{targetEmail}'.");
                return 0; 
            }

            // 3. CONSTRUCT DYNAMIC ENDPOINT URL STRING
            string finalEndpoint = string.Format(EndpointTemplate, borrowerId);
            
            Console.WriteLine($"\n--- [LOG] Starting Microservice Pulse at {DateTime.Now:HH:mm:ss} ---");
            Console.WriteLine($"🌐 URL Target: {finalEndpoint}");

            try
            {
                // FIX: Converted the pipeline network action to an HTTP GET transaction sequence
                var response = await _httpClient.GetAsync(finalEndpoint);
                
                string statusIcon = response.IsSuccessStatusCode ? "✅" : "⚠️";
                Console.WriteLine($"{statusIcon} GET Responses => {(int)response.StatusCode} {response.ReasonPhrase}");

                string responseBody = await response.Content.ReadAsStringAsync();
                
                // 4. PARSE AND EVALUATE ELIGIBILITY STATUS CODES (1 = Eligible, 0 = Not Eligible)
                if (response.IsSuccessStatusCode)
                {
                    if (responseBody.Contains("1") || responseBody.ToLower().Contains("true") || responseBody.ToLower().Contains("eligible"))
                    {
                        Console.WriteLine("🎯 RESULT: 1 = Eligible");
                        return 1;
                    }
                }
                
                Console.WriteLine($"⚠️ RESULT: 0 = Not Eligible. Payload details: {responseBody}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Request execution pipeline failed: {ex.Message}");
                return 0;
            }
            finally
            {
                Console.WriteLine("--- [LOG] Pulse Complete ---\n");
            }
        }

        /// <summary>
        /// Scans target project directories for the newest generated Test_Data_Output CSV data file.
        /// </summary>
        private static string GetEmailFromLatestCsv()
        {
            try
            {
                // 1. Dynamically find the project root from the bin folder
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string outputDirectory = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Data", "Test_Data_Output"));
                
                if (!Directory.Exists(outputDirectory))
                {
                    Console.WriteLine($"❌ Error: Targeted data directory does not exist: {outputDirectory}");
                    return string.Empty;
                }

                // 2. Safely grab the latest CSV from that absolute path
                var directoryInfo = new DirectoryInfo(outputDirectory);
                var targetedFile = directoryInfo.GetFiles("Test_Data_Output_*.csv")
                                                 .OrderByDescending(f => f.LastWriteTime)
                                                 .FirstOrDefault();

                if (targetedFile == null)
                {
                    Console.WriteLine($"❌ Error: No file matching pattern 'Test_Data_Output_*.csv' was found in {outputDirectory}");
                    return string.Empty;
                }

                Console.WriteLine($"📂 Extracting data from latest target file: {targetedFile.Name}");

                var lines = File.ReadAllLines(targetedFile.FullName);
                if (lines.Length <= 1) return string.Empty; 

                var headers = lines[0].Split(',').Select(h => h.Trim().ToLower()).ToList();
                int emailIndex = headers.IndexOf("email");

                if (emailIndex == -1) emailIndex = 0; 

                var genericRowValues = lines[1].Split(',');
                if (genericRowValues.Length > emailIndex)
                {
                    string extractedEmail = genericRowValues[emailIndex].Replace("\"", "").Trim();
                    Console.WriteLine($"📧 Parsed Dynamic Target Email: '{extractedEmail}'");
                    return extractedEmail;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error reading target Data Output CSV: {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Populates parameter values cleanly into the MySqlCommand to secure a single unique BorrowerId.
        /// </summary>
        private static async Task<string> GetBorrowerIdFromDatabaseAsync(string emailSearchValue)
        {
            string borrowerId = string.Empty;

            using (var connection = new MySqlConnection(dbConnectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT borrower.`Id`
                    FROM `Loan`.`Borrower` borrower
                    LEFT JOIN `Account`.`Account` account ON `account`.Id = `borrower`.`AccountId`
                    WHERE `account`.EmailAddress LIKE @EmailParam
                    LIMIT 1;"; 

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@EmailParam", $"%{emailSearchValue}%");
                    
                    var result = await command.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        borrowerId = result.ToString()!;
                        Console.WriteLine($"✅ Database lookup success. Retrieved Borrower ID: {borrowerId}");
                    }
                }
            }

            return borrowerId;
        }

        [Test]
        public static async Task RunTriggerManually()
        {
            int statusResult = await TriggerAllAsync();
            Assert.That(statusResult, Is.EqualTo(1), "Expected borrower pipeline status to return Eligible (1).");
        }
    }
}

// dotnet test --filter "FullyQualifiedName=OpsAdminComplete.Eligibility"