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

        var connectionString = "Host=localhost;Database=Mqtt;Username=postgres;Password=1234";
        DbUpdater dbUpdater = new DbUpdater(connectionString);
        DbChangeTracker dbChangeTracker = new DbChangeTracker(connectionString);

        TimeSpan trackingInterval = TimeSpan.FromMinutes(0.1);

        // Task for MQTT connection
        Task mqttTask = RunMqttClientAsync();

        // Task for database tracking loop
        Task trackingTask = RunTrackingLoopAsync(dbUpdater, dbChangeTracker, trackingInterval);

        await Task.WhenAll(mqttTask, trackingTask);
    }

    private static async Task RunTrackingLoopAsync(DbUpdater dbUpdater, DbChangeTracker dbChangeTracker, TimeSpan trackingInterval)
    {
        while (true)
        {
            try
            {
                string filePath = "C:\\Users\\studentinit\\Documents\\GitHub\\mqtt-test\\db_changes.json";

                // Save database changes to delta file
                await dbChangeTracker.SaveDeltaToFileAsync(filePath);
                //Console.WriteLine("Delta changes saved successfully.");

                // Apply delta to the database 
                await dbUpdater.ApplyChangesAsync(filePath);
            }
            catch (Exception ex)
            {
                Log.Error("An error occurred while tracking changes: {Message}", ex.Message);
            }

            // Wait for the next interval
            await Task.Delay(trackingInterval);
        }
    }

    private static async Task RunMqttClientAsync()
    {
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
            }
            else
            {
                await mqttSubscriberClientService.ConnectAsync();
            }

            // Keeps the MQTT task running indefinitely
            await Task.Delay(Timeout.Infinite);
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
