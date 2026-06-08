using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using OpsAdminComplete;
using System.Text.RegularExpressions;

namespace SalaryFinance.Tests;

[TestFixture]
public class C_OpsAdmin : PageTest
{
    [Test]
    public async Task TriggerAndVerifyAudit()
    {
        Console.WriteLine("🌐 Step C: Syncing Microservices...");
        await MyEndPoints.TriggerAllAsync();
        
        await Page.GotoAsync("https://rc-opsadminui.saldev.net/login", new() { Timeout = 600000 });
        await Page.GetByPlaceholder(new Regex("someone@example.com")).FillAsync("admin.user@salaryfinance.com");
        await Page.Locator("input[type='password']").FillAsync("Pa55w0rd!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();

        await Page.Locator(".v-list-item", new() { HasText = "Reports" }).First.ClickAsync();
        await Page.GetByText("Imported Employees Audit").ClickAsync();

        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Employer" }).FillAsync("RT Proactiv");
        await Page.Locator("div").Filter(new() { HasTextRegex = new Regex("^RT Proactive$") }).Nth(3).ClickAsync();
        
        await Page.GetByRole(AriaRole.Columnheader, new() { Name = "Upload Date: Not sorted." }).ClickAsync();
        await Page.GetByRole(AriaRole.Columnheader, new() { Name = "Upload Date: Sorted ascending" }).ClickAsync();
    


        await MyFunctions.WaitForAuditStatusOne(Page);
        // await MyEndPoints.TriggerAllAsync();
        Console.WriteLine("✅ Step C Complete: Audit Verified.");
    }
}

// $env:PWDEBUG=1; dotnet test --filter "FullyQualifiedName=SalaryFinance.Tests.C_OpsAdmin.TriggerAndVerifyAudit"
// npx playwright codegen https://rc-opsadminui.saldev.net/login