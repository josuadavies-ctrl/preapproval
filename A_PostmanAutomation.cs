using NUnit.Framework;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using Microsoft.Playwright.NUnit;
using Microsoft.Playwright;

namespace PlaywrightTests;

[TestFixture]
public class A_PostmanAutomation : PageTest
{
    [Test]
    public async Task RunPostmanAndThenUiTest()
    {
        string binDir = AppDomain.CurrentDomain.BaseDirectory;
        string statePath = Path.Combine(binDir, "teststate.json");
        
        // 1. LOAD SEEDED DATA
        string stateJson = await File.ReadAllTextAsync(statePath);
        using var doc = JsonDocument.Parse(stateJson);
        string email = doc.RootElement.GetProperty("Email").GetString() ?? "";
        string dobValue = doc.RootElement.GetProperty("DateOfBirth").GetString() ?? "1982-10-21";
        string aggId = Guid.NewGuid().ToString();

        string reportPath = Path.Combine(binDir, "newman-report.json");
        string collectionPath = Path.Combine(binDir, "Test", "postman", "My Collection.postman_collection.json");
        string envPath = Path.Combine(binDir, "Test", "postman", "Staging.postman_environment.json");

        // 2. CONSTRUCT ARGUMENTS WITH THE WORKING DATA SET
        string arguments = $"run \"{collectionPath}\" -e \"{envPath}\" " +
                           $"--env-var \"emailAddress={email}\" " +
                           $"--env-var \"dateOfBirth={dobValue}\" " +
                           $"--env-var \"firstName=Sharon\" " +
                           $"--env-var \"lastName=Rajapaksa\" " +
                           $"--env-var \"title=Ms\" " +
                           $"--env-var \"buildingNumber=80B\" " +
                           $"--env-var \"street=High Street\" " +
                           $"--env-var \"town=St. Albans\" " +
                           $"--env-var \"postCode=AL3 8LE\" " +
                           $"--env-var \"countryCode=GB\" " +
                           $"--env-var \"startDate=2022-01-01\" " +
                           $"--env-var \"aggregator=Go Compare\" " +
                           $"--env-var \"aggregatorRequestId={aggId}\" " +
                           $"--env-var \"employerName=RT Proactive\" " +
                           $"--env-var \"employmentStatus=EmployedFullTime\" " +
                           $"--env-var \"grossAnnualIncome=90000\" " +
                           $"--env-var \"loanAmount=2500\" " +
                           $"--env-var \"loanPurpose=VehicleLoan\" " +
                           $"--env-var \"loanTerm=12\" " +
                           $"--env-var \"maritalStatus=Partner\" " +
                           $"--env-var \"numberOfDependents=1\" " +
                           $"--env-var \"residentialStatus=OwnerNoMortgage\" " +
                           $"--env-var \"workStartDate=2000-01-01\" " +
                           $"--env-var \"propertyCost=0\" " +
                           $"--reporters json --reporter-json-export \"{reportPath}\"";

        // =========================================================================
        // 📦 TERMINAL OUTPUT: PRINT EXPLICIT JSON ENVIROMENT VARIABLE STRUCTURE
        // =========================================================================
        Console.WriteLine("==================================================");
        Console.WriteLine("🚀 SENDING DATA TO NEWMAN (MATCHING SUCCESSFUL MANUAL TEST)");
        Console.WriteLine($"Target Email: {email}");
        Console.WriteLine($"Aggregator Request ID: {aggId}");
        Console.WriteLine("==================================================");

        var payloadObj = new
        {
            additionalEmployerIncome = (object?)null,
            additionalHouseholdIncome = (object?)null,
            addressHistory = new[]
            {
                new
                {
                    abodeNumber = (object?)null,
                    buildingNumber = "80B",
                    buildingName = (object?)null,
                    street = "High Street",
                    town = "St. Albans",
                    countryCode = "GB",
                    postCode = "AL3 8LE",
                    startDate = "2022-01-01",
                    endDate = (object?)null
                }
            },
            aggregator = "Go Compare",
            aggregatorRequestId = aggId,
            changeInFinancialSituation = (object?)null,
            dateOfBirth = dobValue,
            emailAddress = email,
            employerName = "RT Proactive",
            employmentStatus = "EmployedFullTime",
            firstName = "Sharon",
            grossAnnualIncome = 90000,
            grossSalary = (object?)null,
            industry = (object?)null,
            jobTitle = (object?)null,
            lastName = "Rajapaksa",
            loanAmount = 2500,
            loanPurpose = "VehicleLoan",
            loanTerm = 12,
            maritalStatus = "Partner",
            numberOfDependents = 1,
            otherNonEmployerIncome = (object?)null,
            partnerGrossIncome = (object?)null,
            propertyCost = 0,
            residentialStatus = "OwnerNoMortgage",
            title = "Ms",
            workStartDate = "2000-01-01"
        };
        
        string beautifulPayloadJson = JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine("📦 OUTGOING POSTMAN ENVIRONMENT VARIABLES PAYLOAD:");
        Console.WriteLine(beautifulPayloadJson);
        Console.WriteLine("==================================================");

        // 3. EXECUTE NEWMAN
        using var process = Process.Start(new ProcessStartInfo { 
            FileName = "newman.cmd", 
            Arguments = arguments, 
            CreateNoWindow = true, 
            UseShellExecute = false 
        });
        
        if (process != null) await process.WaitForExitAsync();

        // 4. PROCESS RESPONSE
        if (File.Exists(reportPath)) {
            string reportContent = await File.ReadAllTextAsync(reportPath);
            using var reportDoc = JsonDocument.Parse(reportContent);
            var executions = reportDoc.RootElement.GetProperty("run").GetProperty("executions").EnumerateArray();
            
            foreach (var execution in executions) {
                try {
                    var streamData = execution.GetProperty("response").GetProperty("stream").GetProperty("data").EnumerateArray();
                    byte[] bytes = streamData.Select(b => b.GetByte()).ToArray();
                    string decodedBody = Encoding.UTF8.GetString(bytes);

                    Console.WriteLine("==================================================");
                    Console.WriteLine("📡 API RESPONSE FROM NEWMAN");
                    Console.WriteLine(decodedBody);
                    Console.WriteLine("==================================================");

                    using var bodyDoc = JsonDocument.Parse(decodedBody);
                    if (bodyDoc.RootElement.TryGetProperty("applyUrl", out var u)) {
                        string url = u.GetString() ?? "";
                        // Dynamically resolve directory instead of pinning hardcoded local path
                        string projectRoot = Path.GetFullPath(Path.Combine(binDir, "..", "..", ".."));
                        string csvPath = Path.Combine(projectRoot, "Data", "Test Data Output.csv");
                        
                        string dirCheck = Path.GetDirectoryName(csvPath)!;
                        if (!Directory.Exists(dirCheck)) Directory.CreateDirectory(dirCheck);

                        await File.WriteAllTextAsync(csvPath, "applyUrl\n" + url);
                        Console.WriteLine($"✅ URL CAPTURED: {url}");
                        break;
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"⚠️ Response Parsing Error: {ex.Message}");
                }
            }
        }
    }
}