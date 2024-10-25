using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;
using TestApp;

class Program
{
    
    static async Task Main(string[] args)
    {
        
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
        var ConnectionString = "Host=localhost;Database=Mqtt;Username=postgres;Password=1234";



        
        DbUpdater dbUpdater = new DbUpdater(ConnectionString);
        DbChangeTracker dbChangeTracker = new DbChangeTracker(ConnectionString);

        // Seed mock data
        await SeedMockDataAsync(dbUpdater);

        // Example delta JSON for updates
        string deltaJson = @"{
            ""Inserts"": [],
            ""Updates"": [],
            ""Deletes"": []
        }";

        // Call the ApplyChangesAsync method
        try
        {
            await dbUpdater.ApplyChangesAsync(deltaJson);
            Console.WriteLine("Changes applied successfully.");
            await dbChangeTracker.SaveDeltaToFileAsync(ConnectionString);
        }
            
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying changes: {ex.Message}");
        }
    }

    private static async Task SeedMockDataAsync(DbUpdater dbUpdater)
    {
        // Create mock data
        var mockData = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object>
            {
                { "Id", 1 },
                { "RoomName", "Original Name" },
                { "CurrentTemperature", 22 },
                { "CurrentTime", DateTime.UtcNow },
                { "CreatedAt", DateTime.UtcNow },
                { "UpdatedAt", DateTime.UtcNow }
            },
            new Dictionary<string, object>
            {
                { "Id", 2 },
                { "RoomName", "Second Room" },
                { "CurrentTemperature", 20 },
                { "CurrentTime", DateTime.UtcNow },
                { "CreatedAt", DateTime.UtcNow },
                { "UpdatedAt", DateTime.UtcNow }
            }
        };

        // Insert mock data into the database
        //foreach (var row in mockData)
        //{
        //    await dbUpdater.ApplyChangesAsync(JsonConvert.SerializeObject(new Delta
        //    {
        //        Inserts = new List<Dictionary<string, object>> { row },
        //        Updates = new List<Dictionary<string, object>>(),
        //        Deletes = new List<int>()
        //    }));
        //}
        try
        {
            // Read configuration from appsettings.json
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();


            var mqttSettings = new MqttSettings();
            config.GetSection("MqttSettings").Bind(mqttSettings);

            var mqttPublisherClientService = new PublisherClientService(mqttSettings);
            var mqttSubscriberClientService = new SubscriberClientService(mqttSettings);
            if (mqttSettings.Role == "Publisher")
            {
                await mqttPublisherClientService.ConnectAsync();
                await Task.Delay(Timeout.Infinite);
            }
            else
            {
                await mqttSubscriberClientService.ConnectAsync();
                await Task.Delay(Timeout.Infinite);
            }







            //await mqttClientService.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal("An error occurred: {Message}", ex.Message);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
