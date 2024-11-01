using MQTTnet;
using MQTTnet.Client;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using MQTTnet.Protocol;
using TestApp.MqttClientInterfaces;
using System.Threading;
using TestApp;


public class SubscriberClientService : ISubscriber
{
    private readonly MqttSettings _mqttSettings;
    private readonly IMqttClient _mqttClient;
    private readonly IOnFileChanged _onFileChanged;
    private bool _isConnecting;
    private readonly SemaphoreSlim _reconnectSemaphore = new SemaphoreSlim(1, 1);

    public SubscriberClientService(MqttSettings mqttSettings, OnFileChanged onFileChanged)
    {
        _mqttSettings = mqttSettings;
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();
        _onFileChanged = onFileChanged;
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
                

                    await SubscribeAsync(_mqttSettings.Topic);
                    await Task.Delay(3000);

                

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


    // Subscriber implementation
    public async Task SubscribeAsync(string topic)
    {
        await _mqttClient.SubscribeAsync(topic);

        _mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            var payload = e.ApplicationMessage.PayloadSegment.ToArray();
            Log.Information("Subscribed to topic: {Topic}", topic);

            try
            {
                // Is payload db data or something else i can handle
                if (IsDatabasePayload(payload))
                {
                    await _onFileChanged.ReaderDatabaseAsync(payload);
                }
                else
                {
                    await _onFileChanged.ReadFileAsync(payload);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error processing received message: {Message}", ex.Message);
            }
        };
    }

    // Add a method to determine if the payload is intended for the database
    private bool IsDatabasePayload(byte[] payload)
    {
        // Try to convert the payload to a string and check for JSON structure
        string payloadString = Encoding.UTF8.GetString(payload);

        // Simple check to see if it looks like JSON (could also use a more robust method)
        return payloadString.StartsWith("{") && payloadString.EndsWith("}");
    }



    public async Task DisconnectAsync()
    {
        await _mqttClient.DisconnectAsync();
        Log.Information("Disconnected from MQTT broker");
    }
}
