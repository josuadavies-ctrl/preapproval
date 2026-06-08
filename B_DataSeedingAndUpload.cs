using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using System.Text.Json;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SalaryFinance.Tests;

[TestFixture]
public class B_DataSeeding : PageTest
{
    [Test]
    public async Task CreateDataAndUploadToEva()
    {
        // --- DATA GENERATION WITH DYNAMIC DOB OFFSET ---
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string targetFolder = @"C:\Users\JDavies\Playwright\Salary_Finance_(Preapproval)\Data\EVA_Data\EVA-output";
        string evaFile = Path.Combine(targetFolder, $"EVA_Proactive_{timestamp}.csv");

        if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

        Random res = new Random();
        string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string p1 = new string(Enumerable.Repeat(letters, 2).Select(s => s[res.Next(s.Length)]).ToArray());
        string p2 = res.Next(100, 999).ToString();
        string p3 = new string(Enumerable.Repeat(letters, 2).Select(s => s[res.Next(s.Length)]).ToArray());
        string payrollId = $"{p1}{p2}{p3}";

        string firstName = "Sharon";
        string lastName = "Rajapaksa";
        string email = $"sharon.rajapaksa.{payrollId}@saldev.net";

        // Inside B_DataSeedingAndUpload.cs
        DateTime rollingDobContext = DateTime.Today.AddDays(-15911);
        string dobForApi = rollingDobContext.ToString("yyyy-MM-dd"); // e.g., "1982-10-23" for today's run
        string dobForCsv = rollingDobContext.ToString("dd/MM/yyyy"); // e.g., "23/10/1982"

        var state = new { 
            PayrollId = payrollId, 
            DateOfBirth = dobForApi, // <-- This is what downstream steps merge into your output CSV!
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Title = "Ms"
        };
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "teststate.json"), JsonSerializer.Serialize(state));

        // Create EVA CSV matching your layout format exactly
        string content = $"Payroll ID;DOB;Start Date;Payroll Name;Gross Salary;Account Number;Sort Code;Employee Group\n{payrollId};{dobForCsv};01/01/2010;Default;90000;00035305;070116;";
        await File.WriteAllTextAsync(evaFile, content);

        // --- UI AUTOMATION WITH EXPLICIT VISIBILITY WAITS ---
        await Page.GotoAsync("https://sf-eva-staging.salary-finance-ecs-nonprod.com/");
        
        var getStartedBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Get Started" });
        await getStartedBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await getStartedBtn.ClickAsync();

        // Wait specifically for the Microsoft Login Email field to be visible
        var emailInput = Page.Locator("input[type='email'], input[name='loginfmt']");
        await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 600000 });
        await emailInput.FillAsync("kingston@test.co.uk");

        var nextBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Next" });
        if (await nextBtn.IsVisibleAsync()) await nextBtn.ClickAsync();

        // Wait for Password field
        var passwordInput = Page.GetByPlaceholder("Password");
        await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await passwordInput.FillAsync("Pa55w0rd!");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();

        // RT Proactive selected
        var uploadIcon = Page.Locator("span").Nth(22);
        await uploadIcon.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await uploadIcon.ClickAsync();

        var continueBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Continue" });
        await continueBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await continueBtn.ClickAsync();
        
        // File Selection and Encryption
        await Page.SetInputFilesAsync("input[type='file']", evaFile);
        
        var encryptBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Encrypt" });
        await encryptBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await encryptBtn.ClickAsync();

        var finalContinue = Page.GetByRole(AriaRole.Button, new() { Name = "Continue" });
        await finalContinue.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await finalContinue.ClickAsync();

        // Final Upload
        var uploadBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Securely upload" });
        await uploadBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await uploadBtn.ClickAsync(new() { Force = true });
        
        Console.WriteLine($"✅ Seeded User: {email} (ID: {payrollId})");
        await Page.WaitForTimeoutAsync(5000);
    }
}