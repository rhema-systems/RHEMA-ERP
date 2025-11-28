using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestLoginApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

            using var client = new HttpClient(handler);
            var url = "http://localhost:53485/api/auth/login";

            var loginRequest = new
            {
                Username = "admin",
                Password = "Admin123!",
                TenantCode = "DEFAULT"
            };

            try
            {
                Console.WriteLine($"Sending login request to {url}...");
                var json = JsonSerializer.Serialize(loginRequest);
                Console.WriteLine($"Payload: {json}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);

                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Status Code: {response.StatusCode}");
                Console.WriteLine($"Response Body: {responseBody}");

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("token", out var tokenElement))
                    {
                        Console.WriteLine("TOKEN_START");
                        Console.WriteLine(tokenElement.GetString());
                        Console.WriteLine("TOKEN_END");
                    }
                }
                else
                {
                    Console.Error.WriteLine($"Login Failed! Status: {response.StatusCode}");
                    Console.Error.WriteLine(responseBody);
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}
