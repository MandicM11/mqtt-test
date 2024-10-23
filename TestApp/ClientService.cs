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

public class ClientService : IPublisher, ISubscriber
{
    private readonly MqttSettings _mqttSettings;
    private readonly IMqttClient _mqttClient;
    //private readonly HandlePayload _handlePayload;
    private bool _isConnecting;
    private readonly SemaphoreSlim _reconnectSemaphore = new SemaphoreSlim(1, 1);

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
                    // Pass a callback to handle message or file reception
                    await SubscribeAsync(_mqttSettings.Topic);
                    
                }
                else if (_mqttSettings.Role == "Publisher")
                {
                    await PublishAsync(_mqttSettings.Topic, _mqttSettings.PublishedFilePath);
                        
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

    // Implementing PublishAsync 
    public async Task PublishAsync(string topic, object data)
    {
       
        var handlePublisher = new HandlePublisher();
        byte[] payload = await handlePublisher.HandlePayloadAsync(data);
        var mqttMessage = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.PublishAsync(mqttMessage);
        Log.Information("Published data to topic: {Topic}", topic);
    }


    // Subscriber implementation
    public async Task SubscribeAsync(string topic)
    {
        await _mqttClient.SubscribeAsync(topic);

        _mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            var payload = e.ApplicationMessage.PayloadSegment.ToArray();

            Log.Information("Subscribed to topic: {Topic}", topic);

            var handlePayload = new HandleSubscriber(_mqttSettings);
            
            try
                    {
                        await handlePayload.ReadFileAsync(payload);
     
                    }
                catch (Exception ex)
                    {
                    Log.Error("Error processing received message: {Message}", ex.Message);
                    }
            
        };
    }


    
    private bool IsTextPayload(byte[] payload)
    {
        // Check if all bytes are within the ASCII printable range (32 to 126)
        return payload.All(b => b >= 32 && b <= 126);
    }


    public async Task DisconnectAsync()
    {
        await _mqttClient.DisconnectAsync();
        Log.Information("Disconnected from MQTT broker");
    }
}
