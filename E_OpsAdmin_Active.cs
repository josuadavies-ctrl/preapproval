using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework; 
using OpsAdminComplete; // Ensures MyPaymentEndPoint is accessible
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SalaryFinance.Tests;

[TestFixture]
public class E_OpsAdmin_Active : PageTest
{
    [Test]
    [Timeout(900000)] // Grants an explicit 15-minute NUnit framework timeout boundary
    public async Task VerifyLoanActiveStatus()
    {
        Console.WriteLine("🌐 Step E: Verifying Loan Active Status...");

        // 1. Locate the latest Test Data Output CSV
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string outputDirectory = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Data", "Test_Data_Output"));
        
        var directoryInfo = new DirectoryInfo(outputDirectory);
        var latestFile = directoryInfo.GetFiles("Test_Data_Output_*.csv")
                                      .OrderByDescending(f => f.LastWriteTime)
                                      .FirstOrDefault();

        if (latestFile == null)
        {
            Assert.Fail("Could not find any Test_Data_Output CSV files.");
        }

        // 2. Extract Payroll ID from the CSV
        string[] lines = await File.ReadAllLinesAsync(latestFile.FullName);
        if (lines.Length < 2) Assert.Fail("CSV file is empty or missing data row.");
        
        string[] headers = lines[0].Split(',');
        
        int payrollIndex = Array.FindIndex(headers, h => h!.Replace("\"", "").Trim().Equals("payrollId", StringComparison.OrdinalIgnoreCase));
        if (payrollIndex == -1) Assert.Fail("Could not find 'payrollId' column in CSV.");
        
        string payrollId = lines[1].Split(',')[payrollIndex].Replace("\"", "").Trim();
        Console.WriteLine($"🔍 Extracted Payroll ID: {payrollId} from {latestFile.Name}");

        // 3. Login to Ops Admin with an Extended Navigation Timeout for cold server recycles
        Console.WriteLine("🌐 Navigating to Ops Admin Login Page...");
        await Page.GotoAsync("https://rc-opsadminui.saldev.net/login", new() { Timeout = 90000 });
        
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "someone@example.com" }).FillAsync("admin.user@salaryfinance.com");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "someone@example.com" }).PressAsync("Tab");
        
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync("Pa55w0rd!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();

        // 4. Navigate to Customers -> Customer List
        await Page.GetByText("Customers").ClickAsync();
        await Page.GetByText("Customer List").ClickAsync();

        // 5. Input the dynamic Payroll ID once upfront
        var payrollInput = Page.GetByRole(AriaRole.Textbox, new() { Name = "Payroll ID" });
        await Page.Locator("table, .results-grid").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await Task.Delay(2000); 
        await payrollInput.ClearAsync();
        await payrollInput.FillAsync(payrollId);

        // 6. Polling loop: Target the precise grid row containing the Payroll ID
        bool isLoanActive = false;
        int maxRetries = 20;

        for (int i = 0; i < maxRetries; i++)
        {
            Console.WriteLine($"\n📊 Querying Status (Attempt {i + 1}/{maxRetries})...");

            // Click Submit to refresh search results grid structure
            await Page.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
            
            // Allow the table rows a generous 60-second window to resolve and appear in the DOM
            var gridTableRows = Page.Locator("table tbody tr").First;
            await gridTableRows.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 60000 });
            
            // Short stabilization buffer to ensure data values are completely rendered in the UI view
            await Task.Delay(3000);

            // Locate the exact table row belonging to our target borrower
            var customerRow = Page.Locator("table tbody tr").Filter(new() { HasText = payrollId });

            // Extract the text layout contained inside that specific customer row block
            string rowText = await customerRow.CountAsync() > 0 ? await customerRow.InnerTextAsync() : "Empty Grid Layer";
            Console.WriteLine($"📡 Complete UI Row Readout: '{rowText.Trim()}'");

            // EVALUATION STEP A: Check for the ultimate success status within the matched row text
            if (rowText.Contains("Loan Active", StringComparison.OrdinalIgnoreCase))
            {
                isLoanActive = true;
                Console.WriteLine("🎉 Success! Found 'Loan Active' inside the customer row. Escaping pipeline polling sequence.");
                break; 
            }
            // EVALUATION STEP B: Explicitly match the holding state phrase
            else if (rowText.Contains("Awaiting funding disbursal", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("⚠️ Row matched state 'Dec - Awaiting funding disbursal'. Triggering MyPaymentEndPoint backend pulse...");
                await MyPaymentEndPoint.TriggerAllAsync();
                
                Console.WriteLine("⏳ Sleeping 8 seconds for background microservice and DB reconciliation...");
                await Task.Delay(8000);
            }
            // EVALUATION STEP C: Handle microservice text sync lag or blank intermediate layers
            else
            {
                Console.WriteLine("⚠️ Status layout is currently intermediate. Triggering backend fallback triggers...");
                await MyPaymentEndPoint.TriggerAllAsync();
                
                Console.WriteLine("⏳ Sleeping 8 seconds before re-clicking Submit...");
                await Task.Delay(8000);
            }
        }

        // 7. Core Assertion checking overall loop execution outcome status
        Assert.That(isLoanActive, Is.True, $"❌ FAILURE: Loop exhausted 20 retries. The status cell failed to change to 'Loan Active' for User {payrollId}.");
    }
}