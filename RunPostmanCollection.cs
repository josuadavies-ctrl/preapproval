using NUnit.Framework;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright.NUnit;
using Microsoft.Playwright;
using Salary_Finance__Preapproval_.Data;

namespace PlaywrightTests;

[TestFixture]
public class PostmanAutomation : PageTest
{
    private static readonly SemaphoreSlim _csvLock = new SemaphoreSlim(1, 1);

    [Test]
    public async Task RunPostmanAndThenUiTest()
    {
        // --- 1. DATA GENERATION ---
        var user = TestData.GenerateUser();
        string requestId = Guid.NewGuid().ToString();
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string testName = $"Postman_{timestamp}";
        
        string dob = "1980-11-13", title = "Mr", mobile = "07412345678";
        string password = "Pa55w0rd!123", sortCode = "085506", accNumber = "29250953";

        // --- 2. PATH SETUP ---
        string binDir = AppDomain.CurrentDomain.BaseDirectory;
        string dataFolder = Path.Combine(binDir, "Data");
        if (!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);
        
        string csvPath = Path.Combine(dataFolder, "UsedTestData.csv");
        string collectionPath = Path.Combine(binDir, "Test", "postman", "My Collection.postman_collection.json");
        string envPath = Path.Combine(binDir, "Test", "postman", "Staging.postman_environment.json");
        string reportPath = Path.Combine(binDir, "newman-report.json");

        // --- 3. RUN NEWMAN ---
        string arguments = $"run \"{collectionPath}\" -e \"{envPath}\" " +
                           $"--env-var \"firstName={user.FirstName}\" " +
                           $"--env-var \"lastName={user.LastName}\" " +
                           $"--env-var \"emailAddress={user.Email}\" " +
                           $"--env-var \"aggregatorRequestId={requestId}\" " +
                           $"--reporters json --reporter-json-export \"{reportPath}\"";

        string newmanExecutable = OperatingSystem.IsWindows() ? "newman.cmd" : "newman";

        var startInfo = new ProcessStartInfo
        {
            FileName = newmanExecutable, 
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true
        };

        using (Process process = Process.Start(startInfo)!)
        {
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) Assert.Fail($"Newman failed with code {process.ExitCode}");
        }

        // --- 4. EXTRACT DATA ---
        string reportContent = await File.ReadAllTextAsync(reportPath);
        using var doc = JsonDocument.Parse(reportContent);
        var executions = doc.RootElement.GetProperty("run").GetProperty("executions").EnumerateArray();
        var preEx = executions.First(e => e.GetProperty("item").GetProperty("name").GetString() == "Preapproval Staging");

        byte[] bytes = preEx.GetProperty("response").GetProperty("stream").GetProperty("data").EnumerateArray().Select(b => b.GetByte()).ToArray();
        string decodedBody = Encoding.UTF8.GetString(bytes);
        
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        string prettyResponse = JsonSerializer.Serialize(JsonDocument.Parse(decodedBody).RootElement, jsonOptions);

        TestContext.Progress.WriteLine("--------------------------------------------------");
        TestContext.Progress.WriteLine($"📤 REQUEST FOR: {user.Email}");
        TestContext.Progress.WriteLine($"📥 RESPONSE:\n{prettyResponse}");
        TestContext.Progress.WriteLine("--------------------------------------------------");

        string applyUrl = JsonDocument.Parse(decodedBody).RootElement.GetProperty("applyUrl").GetString() 
                          ?? throw new Exception("ApplyUrl missing from API response.");

        // --- 5. LOG TO CSV ---
        var csvLine = $"{testName},{applyUrl},{title},{user.FirstName},{user.LastName},{dob},{user.PayrollId},{mobile},{user.Email},{password},{sortCode},{accNumber}{Environment.NewLine}";

        await _csvLock.WaitAsync();
        try
        {
            if (!File.Exists(csvPath) || new FileInfo(csvPath).Length == 0)
            {
                string header = "TestName,URL,Title,FirstName,LastName,DOB,PayrollID,MobileNumber,Email,Password,SortCode,AccNumber" + Environment.NewLine;
                await File.WriteAllTextAsync(csvPath, header, Encoding.UTF8);
            }
            await File.AppendAllTextAsync(csvPath, csvLine, Encoding.UTF8);
        }
        finally { _csvLock.Release(); }

        // --- 6. PLAYWRIGHT UI TEST ---
        TestContext.Progress.WriteLine($"🌐 NAVIGATING TO UI: {applyUrl}");
        
        await Page.GotoAsync(applyUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });

        // Error detection logic (Triggered in your last run)
        if (Page.Url.Contains("InternalServerError"))
        {
            string screenshotPath = Path.Combine(binDir, $"Error_{timestamp}.png");
            await Page.ScreenshotAsync(new() { Path = screenshotPath });
            TestContext.AddTestAttachment(screenshotPath);
            Assert.Fail($"Environment Error: The site redirected to an Internal Server Error page. URL: {Page.Url}");
        }

        await Expect(Page).ToHaveURLAsync(new Regex(".*salaryfinance\\.(net|club).*"), new() { Timeout = 15000 });

        TestContext.Progress.WriteLine("✅ Navigation successful.");
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        _csvLock.Dispose();
    }
}

// Open recorder ----------- npx playwright codegen https://rc-borrowui.saldev.net/app-rec/BorrowHome
// Open Ops Admin Recorder ----------- npx playwright codegen https://rc-opsadminui.saldev.net/login

// dotnet test --filter "FullyQualifiedName~PlaywrightTests.PostmanAutomation"
// $env:HEADLESS="false"; dotnet test



// Top 3 things to check in your environment/data:

//Employer Mapping: Ensure that employerName: "Kingston" in your Postman body matches a valid, active employer in the kingstonnhs.salaryfinance.club silo.

// Payroll ID Conflict: Sometimes staging crashes if the PayrollId (e.g., ADF125) doesn't exist in the "Employee" table for that specific employer.

// Authentication/Redirect Tokens: The applyUrl contains a GUID. If the session service in Staging is down, it won't be able to resolve that GUID into a user session.