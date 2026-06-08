using System;
using System.Linq;

namespace Salary_Finance__Preapproval_.Data;

public static class TestData
{
    // 50 Random Male First Names
    public static readonly string[] MaleFirstNames = 
    { 
        "James", "John", "Robert", "Michael", "William", "David", "Richard", "Joseph", "Thomas", "Charles", 
        "Christopher", "Daniel", "Matthew", "Anthony", "Mark", "Donald", "Steven", "Paul", "Andrew", "Joshua", 
        "Kenneth", "Kevin", "Brian", "George", "Timothy", "Ronald", "Edward", "Jason", "Jeffrey", "Gary", 
        "Jacob", "Nicholas", "Eric", "Jonathan", "Stephen", "Larry", "Justin", "Scott", "Brandon", "Benjamin", 
        "Samuel", "Gregory", "Alexander", "Frank", "Patrick", "Raymond", "Jack", "Dennis", "Jerry", "Tyler" 
    };

    // 50 Random Last Names
    public static readonly string[] LastNames = 
    { 
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", 
        "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin", 
        "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson", 
        "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores", 
        "Green", "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell", "Carter", "Roberts" 
    };

    /// <summary>
    /// Generates a random set of user data including a specific Email format and Payroll ID.
    /// </summary>
    /// <returns>A tuple containing FirstName, LastName, Email, and PayrollId</returns>
    public static (string FirstName, string LastName, string Email, string PayrollId) GenerateUser()
    {
        var rand = new Random();
        // Added timestamp for uniqueness
        string ts = DateTime.Now.ToString("HHmm");
        
        // 1. Pick Names
        string fName = MaleFirstNames[rand.Next(MaleFirstNames.Length)];
        string lName = LastNames[rand.Next(LastNames.Length)];
        
        // 2. Generate Email: f.lastname + timestamp + random digits @sf.com
        string email = $"{fName.Substring(0, 1).ToLower()}.{lName.ToLower()}{ts}{rand.Next(10, 99)}@sf.com";
        
        // 3. Generate Payroll ID: 2 Alpha characters + timestamp + 2 Numbers
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string payrollPrefix = new string(Enumerable.Repeat(chars, 2)
            .Select(s => s[rand.Next(s.Length)]).ToArray());
        
        string payrollId = $"{payrollPrefix}{ts}{rand.Next(10, 99)}";

        return (fName, lName, email, payrollId);
    }
}