using Microsoft.Playwright;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpsAdminComplete
{
    public static class MyFunctions
    {
        // Sharon's Data Profile
        public static dynamic GenerateRandomBorrower()
        {
            // Dynamically calculates the rolling date context based on your offset formula
            string dynamicDob = DateTime.Today.AddDays(-15911).ToString("dd/MM/yyyy");

            return new {
                FirstName = "Sharon",
                LastName = "Rajapaksa",
                PayrollId = "RT-PRO-001",
                Email = $"sharon.rajapaksa_{DateTime.Now:yyyyMMdd_HHmm}@saldev.net",
                Phone = "07475352330",
                Dob = dynamicDob, // <-- Updates automatically every single day
                StartDate = "01/2010",
                GrossSalary = "90000",
                JobTitle = "Automation Engineer",
                Postcode = "AL3 8LE",
                Address = "80B, High Street, St. Albans",
                Title = "Ms"
            };
        }

        // Logic for Ops Admin Sync
        public static async Task WaitForAuditStatusOne(IPage Page)
        {
            Console.WriteLine("[LOG] Polling Ops Admin for Audit Status '1'...");
            DateTime stopTime = DateTime.Now.AddMinutes(25);
            
            while (DateTime.Now < stopTime)
            {
                if (Page.IsClosed) return;
                try 
                {
                    var rows = Page.Locator("tbody tr");
                    await rows.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                    var statusValue = (await rows.First.Locator("td").Nth(11).InnerTextAsync()).Trim();
                    
                    if (statusValue.Contains("1")) return;
                } 
                catch { /* Transient UI error */ }

                await Page.WaitForTimeoutAsync(10000); 
                await Page.ReloadAsync();
            }
            Assert.Fail("Timed out waiting for Audit Status '1'.");
        }

        // CSV Parser for UI Handover
        public static string GetEmailFromLatestReport()
        {
            string directoryPath = @"C:\Users\JDavies\Playwright\Salary_Finance_(Preapproval)\Data\Reports-output\";
            var directory = new DirectoryInfo(directoryPath);
            var latestFile = directory.GetFiles("*.csv").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
            if (latestFile == null) return "manual.test@saldev.net"; // Fallback

            var lines = File.ReadAllLines(latestFile.FullName);
            var dataLine = lines.Skip(1).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
            if (dataLine == null) return "manual.test@saldev.net";

            var columns = dataLine.Split(',');
            return columns.Length > 25 ? columns[25].Trim('"').Trim() : columns[0].Trim('"');
        }
    }
}