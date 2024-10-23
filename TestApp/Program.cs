using Microsoft.Extensions.Configuration;
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

        try
        {
            // Read configuration from appsettings.json
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();


            var mqttSettings = new MqttSettings();
            config.GetSection("MqttSettings").Bind(mqttSettings);

            var mqttClientService = new ClientService(mqttSettings);
            await mqttClientService.ConnectAsync();
            await Task.Delay(Timeout.Infinite);

           




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
