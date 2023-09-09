using Azure.Core;
using Azure.Identity;
using Newtonsoft.Json.Linq;
using SessionDemo.Models;
using System.Text;
using System.Text.Json;

class Program
{
    private const string BaseUrl = "https://brazilus.management.azure.com";

    public static async Task Main()
    {
        var program = new Program();
        await program.Run();
    }

    public async Task Run()
    {
        // Get azure token
        var credential = new InteractiveBrowserCredential();
        string token = (await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }))).Token;
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        Console.Write("Enter the subscription: ");
        string subscription = Console.ReadLine();

        Console.Write("Enter the resource group name: ");
        string rg = Console.ReadLine();

        Console.Write("Enter the environment name: ");
        string environmentName = Console.ReadLine();

        Console.Write("Enter the location, e.g. North Central US (Stage): ");
        string location = Console.ReadLine();

        bool envCreated = await CreateEnvironmentAsync(environmentName, subscription, rg, location, client);
        if (!envCreated)
        {
            return;
        }

        Console.Write($"Successfully created environment {environmentName}, please enter the name to create a session pool: ");
        string sessionPoolName = Console.ReadLine();

        bool sessionPoolCreated = await CreateSessionPoolAsync(sessionPoolName, environmentName, subscription, rg, location, client);
        if (!sessionPoolCreated)
        {
            return;
        }

        await GenerateSessionsAsync(sessionPoolName, subscription, rg, location, client);
    }

    private async Task<bool> CreateEnvironmentAsync(
        string environmentName,
        string subscription,
        string rg,
        string location,
        HttpClient client)
    {
        Console.WriteLine($"Starting creating environment {environmentName}.");
        var environmentUrl = $"{BaseUrl}/subscriptions/{subscription}/resourceGroups/{rg}/providers/Microsoft.App/managedEnvironments/{environmentName}?api-version=2023-05-02-preview";
        var environmentPayload = new
        {
            location = location,
            properties = new
            {
                workloadProfiles = new[]
                {
                    new
                    {
                        workloadProfileType = "Consumption",
                        name = "Consumption"
                    }
                },
                appLogsConfiguration = (object)null
            }
        };

        var environmentResponse = await client.PutAsync(environmentUrl, new StringContent(JsonSerializer.Serialize(environmentPayload), Encoding.UTF8, "application/json"));
        string environmentResponseContent = await environmentResponse.Content.ReadAsStringAsync();
        if (environmentResponse.IsSuccessStatusCode)
        {
            JObject environmentResponseParsed = JObject.Parse(environmentResponseContent);
            string provisioningState = environmentResponseParsed["properties"]["provisioningState"].ToString();
            Console.WriteLine($"The current environment state is {provisioningState}");

            DateTime startTime = DateTime.UtcNow;
            TimeSpan timeout = TimeSpan.FromMinutes(3);

            while (!string.Equals(provisioningState, "Succeeded", StringComparison.CurrentCultureIgnoreCase) && (DateTime.UtcNow - startTime) < timeout)
            {
                await Task.Delay(5000); // Wait for 5 second

                var pollingResponse = await client.GetAsync(environmentUrl);
                string pollingResponseContent = await pollingResponse.Content.ReadAsStringAsync();
                if (pollingResponse.IsSuccessStatusCode)
                {
                    JObject pollingResponseParsed = JObject.Parse(pollingResponseContent);
                    provisioningState = pollingResponseParsed["properties"]["provisioningState"].ToString();

                    Console.WriteLine($"The current environment state is {provisioningState}");
                }
                else
                {
                    Console.WriteLine($"Failed to poll environment with status code: {pollingResponse.StatusCode}, with message: {pollingResponseContent}");
                    break; // Exit the loop if the polling request fails
                }
            }

            if (!string.Equals(provisioningState, "Succeeded", StringComparison.CurrentCultureIgnoreCase))
            {
                Console.WriteLine($"Timed out waiting for environment to reach 'Succeeded' state after 3 minutes. The current state is {provisioningState}");
                return false;
            }

            return true;
        }
        else
        {
            Console.WriteLine($"Failed to create environment with status code: {environmentResponse.StatusCode}, with message: {environmentResponseContent}");
            return false;
        }
    }

    private List<KeyValuePair<string, string>> GetSecretsFromInput()
    {
        Console.WriteLine($"Enter the sessionpool secrets. Type ` to quit: ");
        var secrets = new List<KeyValuePair<string, string>>();
        int i = 1;

        while (true)
        {
            Console.Write($"Enter name for secret {i} (or ` to quit):");
            string name = Console.ReadLine();

            if (name == "`")
                break;

            Console.Write($"Enter value for secret {i} (or ` to quit):");
            string value = Console.ReadLine();

            if (value == "`")
                break;

            secrets.Add(new KeyValuePair<string, string>(name, value));
            i++;
        }

        return secrets;
    }

    private async Task<bool> CreateSessionPoolAsync(
        string sessionPoolName,
        string environmentName,
        string subscription,
        string rg,
        string location,
        HttpClient client)
    {
        Console.Write("Enter the count of max concurrent sessions under the sessionpool(1 - 1000): ");
        int maxConcurrentSession;
        if (!int.TryParse(Console.ReadLine(), out maxConcurrentSession))
        {
            Console.WriteLine("Invalid count entered. Please enter a valid number.");
            return false;
        }

        var inputSecrets = GetSecretsFromInput();
        var sessionPoolSecrets = new object[inputSecrets.Count];
        for (int j = 0; j < inputSecrets.Count; j++)
        {
            sessionPoolSecrets[j] = new
            {
                name = inputSecrets[j].Key,
                value = inputSecrets[j].Value
            };
        }

        var sessionPoolUrl = $"{BaseUrl}/subscriptions/{subscription}/resourceGroups/{rg}/providers/Microsoft.App/sessionpools/{sessionPoolName}?api-version=2023-08-01-preview";
        var sessionPoolPayload = new
        {
            location = location,
            properties = new
            {
                managedEnvironmentId = $"/subscriptions/{subscription}/resourceGroups/{rg}/providers/Microsoft.App/managedEnvironments/{environmentName}",
                maxConcurrentSessions = maxConcurrentSession,
                name = sessionPoolName,
                sessionPoolSecrets = sessionPoolSecrets
            }
        };

        var seesionPoolResponse = await client.PutAsync(sessionPoolUrl, new StringContent(JsonSerializer.Serialize(sessionPoolPayload), Encoding.UTF8, "application/json"));
        if (seesionPoolResponse.IsSuccessStatusCode)
        {
            return true;
        }
        else
        {
            Console.WriteLine($"Failed to create sessionpool with status code: {seesionPoolResponse.StatusCode}, with message: {await seesionPoolResponse.Content.ReadAsStringAsync()}");
            return false;
        }
    }

    private async Task GenerateSessionsAsync(
        string sessionPoolName,
        string subscription,
        string rg,
        string location,
        HttpClient client)
    {
        while (true)
        {
            Console.Write($"Enter the count of sessions to be generated. Type 'exit' to quit: ");
            string input = Console.ReadLine();
            if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
            {
                break;  // Exit the loop if the user types 'exit'
            }

            int count;
            if (!int.TryParse(input, out count))
            {
                Console.WriteLine("Invalid count entered. Please enter a valid number.");
                return;
            }

            var sessionUrl = $"{BaseUrl}/subscriptions/{subscription}/resourceGroups/{rg}/providers/Microsoft.App/sessionPools/{sessionPoolName}/generateSessions?count={count}&api-version=2023-08-01-preview";

            Console.Write($"Enter the image to be used: ");
            var image = Console.ReadLine();

            Console.Write($"Enter the exposed port: ");
            int port;
            if (!int.TryParse(Console.ReadLine(), out port))
            {
                Console.WriteLine("Invalid count entered. Please enter a valid number.");
                return;
            }

            Console.Write($"Enter the expiry time in seconds: ");
            int requestedDurationInSeconds;
            if (!int.TryParse(Console.ReadLine(), out requestedDurationInSeconds))
            {
                Console.WriteLine("Invalid expiry time entered. Please enter a valid number.");
                return;
            }

            var sessionPayload = new
            {
                location = location,
                properties = new
                {
                    requestedDurationInSeconds = requestedDurationInSeconds,
                    sessionKind = "CustomImage",
                    sessionIngress = new { targetPort = port },
                    customContainerConfiguration = new
                    {
                        sessionContainers = new[]
                        {
                            new
                            {
                                image = image,
                                name = "mycontainer",
                                // env = new[] { new { name = "test", value = "testval" } },
                                resources = new { cpu = 0.25, memory = "0.5Gi" }
                            }
                        }
                    }
                }
            };

            var sessionResponse = await client.PostAsync(sessionUrl, new StringContent(JsonSerializer.Serialize(sessionPayload), Encoding.UTF8, "application/json"));
            if (sessionResponse.IsSuccessStatusCode)
            {
                string content = await sessionResponse.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // Deserialize the JSON content to the ApiResponse object
                ApiResponse apiResponse = JsonSerializer.Deserialize<ApiResponse>(content, options);

                if (apiResponse?.Value != null)
                {
                    List<Session> sessions = apiResponse.Value.Select(v => v.Properties).ToList();
                    if (sessions != null)
                    {
                        for (int i = 0; i < sessions.Count; i++)
                        {
                            Console.WriteLine($"Session{i + 1} endpoint: {sessions[i].Endpoint}");
                        }
                    }
                }

                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"POST request failed with status code: {sessionResponse.StatusCode}, with message: {await sessionResponse.Content.ReadAsStringAsync()}");
            }
        }
    }
}