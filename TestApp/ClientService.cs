using MQTTnet;
using MQTTnet.Client;
using System;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using TestApp;

public class ClientService
{
    private readonly MqttSettings _mqttSettings;
    private readonly IMqttClient _mqttClient;
    private bool _isConnecting;

    public ClientService(MqttSettings mqttSettings)
    {
        _mqttSettings = mqttSettings;
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        // Attach the Disconnected event handler
        _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;
    }

    public async Task ConnectAsync()
    {
        await TryConnectAsync();
    }

    private async Task TryConnectAsync()
    {
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_mqttSettings.Broker, _mqttSettings.Port)
            .WithClientId(Guid.NewGuid().ToString())
            .WithCredentials(_mqttSettings.Username, _mqttSettings.Password)
            .Build();

        try
        {
            var connectResult = await _mqttClient.ConnectAsync(options);
            if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
            {
                Log.Information("Connected to MQTT broker as {Role}", _mqttSettings.Role);

                if (_mqttSettings.Role == "Subscriber")
                {
                    await SubscribeAsync();
                }
                else if (_mqttSettings.Role == "Publisher")
                {
                    await PublishAsync("Hello!");
                }
            }
            else
            {
                Log.Error("Failed to connect to MQTT broker: {ResultCode}", connectResult.ResultCode);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Error connecting to MQTT broker: {Message}", ex.Message);
        }
    }

    private async Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        Log.Warning("Disconnected from MQTT broker. Attempting to reconnect...");
        Log.Information("Client State: {State}", _mqttClient.IsConnected ? "Connected" : "Disconnected");
        await ReconnectAsync();



    }

    private async Task ReconnectAsync()
    {
        while (!_isConnecting)
        {
            try
            {
                if (_mqttClient.IsConnected)
                {

                    Log.Information("Client is already connected.");
                    return;
                }

                Log.Information("Attempting to reconnect...");
                _isConnecting = true;
                await Task.Delay(3000); // Wait before trying to reconnect
                await TryConnectAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Reconnect attempt failed: {Message}", ex.Message);
            }
            finally
            {
                _isConnecting = false;
            }
        }
    }

    private async Task SubscribeAsync()
    {
        await _mqttClient.SubscribeAsync(_mqttSettings.Topic);
        Log.Information("Subscribed to topic: {Topic}", _mqttSettings.Topic);

        _mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            Log.Information("Received message: {Message}", Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
            return Task.CompletedTask;
        };
    }

    private async Task PublishAsync(string message)
    {
        var mqttMessage = new MqttApplicationMessageBuilder()
            .WithTopic(_mqttSettings.Topic)
            .WithPayload(message)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag()
            .Build();

        for (int i = 0; i < 5; i++)
        {
            await _mqttClient.PublishAsync(mqttMessage);
            Log.Information("Published message: {Message}", message);
            await Task.Delay(1000);
        }
    }

    public async Task DisconnectAsync()
    {
        await _mqttClient.DisconnectAsync();
        Log.Information("Disconnected from MQTT broker");
    }
}
