using MQTTnet;
using MQTTnet.Client;
using System;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using TestApp;
using MQTTnet.Protocol;

public class ClientService : TestApp.MqttClientInterfaces.IPublisher, TestApp.MqttClientInterfaces.ISubscriber
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

    private readonly SemaphoreSlim _reconnectSemaphore = new SemaphoreSlim(1, 1);

    private async Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        Log.Warning("Disconnected from MQTT broker. Attempting to reconnect...");

        // Only allow one reconnection attempt at a time
        if (_reconnectSemaphore.CurrentCount == 0)
        {
            Log.Information("Reconnection already in progress.");
            return;
        }

        await _reconnectSemaphore.WaitAsync();
        try
        {
            await ReconnectAsync();
        }
        finally
        {
            _reconnectSemaphore.Release();
        }
    }

    private async Task ReconnectAsync()
    {
        while (!_isConnecting)
        {
            try
            {
                if (_mqttClient.IsConnected)
                {
                    // Log.Information("Client is already connected.");
                    return; // Exit if already connected
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

    public async Task PublishFileAsync(string topic, string filePath)
    {
        if (!File.Exists(filePath))
        {
            Log.Error("File does not exist: {FilePath}", filePath);
            return;
        }

        var fileBytes = await File.ReadAllBytesAsync(filePath);

        var mqttMessage = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(fileBytes)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.PublishAsync(mqttMessage);
        Log.Information("Published file: {FilePath} to topic: {Topic}", filePath, topic);
    }

    // Subscribe and receive file, then save it
    public async Task SubscribeToFileAsync(string topic, string saveFilePath)
    {
        await _mqttClient.SubscribeAsync(topic);

        _mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            var fileBytes = e.ApplicationMessage.PayloadSegment.ToArray();
            await File.WriteAllBytesAsync(saveFilePath, fileBytes);
            Log.Information("Received file and saved to: {SaveFilePath}", saveFilePath);
        };

        Log.Information("Subscribed to topic: {Topic}", topic);
    }

    public async Task DisconnectAsync()
    {
        await _mqttClient.DisconnectAsync();
        Log.Information("Disconnected from MQTT broker");
    }
}
