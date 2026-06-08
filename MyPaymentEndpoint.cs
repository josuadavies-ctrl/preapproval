using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpsAdminComplete
{
    [TestFixture]
    public static class MyPaymentEndPoint
    {
        // Suppress NUnit warnings for static field lifecycle management
#pragma warning disable NUnit1032
        private static HttpClient? _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };
#pragma warning restore NUnit1032

        // These endpoints force the 5-minute scheduled tasks to run immediately
        private static readonly string[] Endpoints = new[]
        {
            "https://rc-payment.saldev.net/api/ScheduledActions/executions?runInterval=fiveMinutes&apiKey=c10cc286-9877-4a3b-a12c-2cff2c7005dc"
        };

        [OneTimeTearDown]
        public static void Cleanup()
        {
            _httpClient?.Dispose();
            _httpClient = null;
        }

        /// <summary>
        /// Orchestrates a POST request to all microservice triggers.
        /// Used to sync EVA uploads and Pre-approval data immediately.
        /// </summary>
        public static async Task TriggerAllAsync()
        {
            // Re-initialize if the client was disposed by a previous test teardown
            _httpClient ??= new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

            Console.WriteLine($"\n--- [LOG] Starting Microservice Pulse at {DateTime.Now:HH:mm:ss} ---");

            foreach (var endpoint in Endpoints)
            {
                try
                {
                    var uri = new Uri(endpoint);
                    using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
                    
                    var response = await _httpClient.PostAsync(endpoint, content);
                    
                    string statusIcon = response.IsSuccessStatusCode ? "✅" : "⚠️";
                    Console.WriteLine($"{statusIcon} POST {uri.Host} => {(int)response.StatusCode} {response.ReasonPhrase}");

                    if (!response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"   Detail: {body}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Request failed for {endpoint}: {ex.Message}");
                }
            }
            Console.WriteLine("--- [LOG] Pulse Complete ---\n");
        }

        // Allows NUnit to run this as a standalone test if needed
        [Test]
        public static async Task RunTriggerManually()
        {
            await TriggerAllAsync();
        }
    }
}

// await MyPaymentEndPoint.TriggerAllAsync();
// dotnet test --filter "Name=TriggerMyPaymentEndPoint" --no-restore