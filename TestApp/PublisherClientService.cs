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


public class PublisherClientService : IPublisher
{
    private readonly MqttSettings _mqttSettings;
    private readonly IMqttClient _mqttClient;
    //private readonly HandlePayload _handlePayload;
    private bool _isConnecting;
    private readonly SemaphoreSlim _reconnectSemaphore = new SemaphoreSlim(1, 1);

    public PublisherClientService(MqttSettings mqttSettings)
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
                while (true)
                {

                    await PublishAsync(_mqttSettings.Topic, _mqttSettings.DbChangesFilePath);
                    await Task.Delay(3000);

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

    // Publisher Implementation 
    public async Task PublishAsync(string topic, object data)
    {


        var handlePublisher = new HandlePublisher(_mqttSettings);
        byte[] payload = await handlePublisher.HandlePayloadAsync(data);
        byte[] lastPublishedPayload;


        lastPublishedPayload = await File.ReadAllBytesAsync(_mqttSettings.LocalFilePath);
        bool fileChange = await handlePublisher.filesChangedAsync(payload, lastPublishedPayload);
        Log.Information("fileChange: {FileChange}", fileChange);
        if (fileChange)
        {
            Log.Information("We have a change so we are publishing");
            var mqttMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();


            await _mqttClient.PublishAsync(mqttMessage);
            await File.WriteAllBytesAsync(_mqttSettings.LocalFilePath, payload);
            Log.Information("Published data to topic: {Topic}", topic);


        }

    }
   
    public async Task DisconnectAsync()
    {
        await _mqttClient.DisconnectAsync();
        Log.Information("Disconnected from MQTT broker");
    }
}
