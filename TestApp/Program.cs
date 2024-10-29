using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
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

        // Load configuration from appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Bind the MqttSettings section to the MqttSettings class
        var mqttSettings = config.GetSection("MqttSettings").Get<MqttSettings>();
        var connectionString = mqttSettings.PublisherConnectionString;  // Depending on the role
        if (string.IsNullOrEmpty(connectionString))
        {
            Log.Error("Connection string is null or empty.");
            return;
        }
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString); // Use the dynamically loaded connection string

        var appDbContext = new AppDbContext(optionsBuilder.Options, connectionString);
        

        // Pass the connection string to DbUpdater and DbChangeTracker
        DbUpdater dbUpdater = new(appDbContext);
        DbChangeTracker dbChangeTracker = new(appDbContext);
        await dbUpdater.ApplyChangesAsync(appDbContext, mqttSettings.DbChangesFilePath);



        TimeSpan trackingInterval = TimeSpan.FromMinutes(0.2);

        // Task for MQTT connection
        Task mqttTask = RunMqttClientAsync(mqttSettings);

        // Task for database tracking loop
        Task trackingTask = RunTrackingLoopAsync(dbUpdater, dbChangeTracker, trackingInterval, mqttSettings, connectionString);

        await Task.WhenAll(mqttTask, trackingTask);
    }

    private static async Task RunTrackingLoopAsync(DbUpdater dbUpdater, DbChangeTracker dbChangeTracker, TimeSpan trackingInterval, MqttSettings mqttSettings, string connectionString)
    {
        var _optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        while (true)
        {
            try
            {

                using var appDbContext = new AppDbContext(_optionsBuilder.Options, connectionString);
                await dbChangeTracker.SaveDeltaToFileAsync(mqttSettings.DbChangesFilePath);
                await dbUpdater.ApplyChangesAsync(appDbContext, mqttSettings.DbChangesFilePath);
                
            }
            catch (Exception ex)
            {
                Log.Error("An error occurred while tracking changes: {Message}", ex.Message);
            }

            await Task.Delay(trackingInterval);
        }
    }

    private static async Task RunMqttClientAsync(MqttSettings mqttSettings)
    {
        try
        {
            var onFileChanged = new OnFileChanged(mqttSettings);
            var fileChanged = new FileChanged(mqttSettings);

            var mqttPublisherClientService = new PublisherClientService(mqttSettings, fileChanged);
            var mqttSubscriberClientService = new SubscriberClientService(mqttSettings, onFileChanged);

            if (mqttSettings.Role == "Publisher")
            {
                await mqttPublisherClientService.ConnectAsync();
            }
            else
            {
                await mqttSubscriberClientService.ConnectAsync();
            }

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
