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
        var connectionString = "";
        Log.Information("Role is: {role}", mqttSettings.Role);
        if (mqttSettings.Role == "Publisher")
        {
            connectionString = mqttSettings.PublisherConnectionString;
        }
        else
        {
            connectionString = mqttSettings.SubscriberConnectionString;
        }
        if (string.IsNullOrEmpty(connectionString))
        {
            Log.Warning("Connection string is missing. Running in file-handling mode.");

            // Run file-handling mode instead of database operations
            await RunFileHandlingModeAsync(mqttSettings);
        }
        else
        {
            // Set up DbContext with the connection string
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(connectionString);
            var appDbContext = new AppDbContext(optionsBuilder.Options, connectionString);

            // Run tasks with database access
            DbUpdater dbUpdater = new(appDbContext);
            DbChangeTracker dbChangeTracker = new(appDbContext);

            // Task for MQTT connection
            await RunMqttClientAsync(mqttSettings, appDbContext);

            // Uncomment if tracking loop is required
            // TimeSpan trackingInterval = TimeSpan.FromMinutes(0.2);
            // Task trackingTask = RunTrackingLoopAsync(dbUpdater, dbChangeTracker, trackingInterval, mqttSettings, connectionString);
            // await Task.WhenAll(mqttTask, trackingTask);
        }
    }

    private static async Task RunFileHandlingModeAsync(MqttSettings mqttSettings)
    {
        try
        {
            
            var onFileChanged = new OnFileChanged(mqttSettings, null);
            var fileChanged = new FileChanged(mqttSettings, null);

            var mqttPublisherClientService = new PublisherClientService(mqttSettings, fileChanged);
            var mqttSubscriberClientService = new SubscriberClientService(mqttSettings, onFileChanged);

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
        }
        catch (Exception ex)
        {
            Log.Fatal("An error occurred in file-handling mode: {Message}", ex.Message);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static async Task RunMqttClientAsync(MqttSettings mqttSettings, AppDbContext appDbContext)
    {
        try
        {
            var onFileChanged = new OnFileChanged(mqttSettings, appDbContext);
            var fileChanged = new FileChanged(mqttSettings, appDbContext);

            var mqttPublisherClientService = new PublisherClientService(mqttSettings, fileChanged);
            var mqttSubscriberClientService = new SubscriberClientService(mqttSettings, onFileChanged);

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