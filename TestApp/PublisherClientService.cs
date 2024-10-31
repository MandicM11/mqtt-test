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
using MQTTnet.Exceptions;
using System.Collections;


public class PublisherClientService : IPublisher
{
    private readonly MqttSettings _mqttSettings;
    private readonly IMqttClient _mqttClient;
    private readonly FileChanged _filechanged;
    private readonly OnFileChanged _onFileChanged;
    private bool _isConnecting;
    private readonly SemaphoreSlim _reconnectSemaphore = new SemaphoreSlim(1, 1);

    public PublisherClientService(MqttSettings mqttSettings, FileChanged filechanged, OnFileChanged onFileChanged)
    {
        _mqttSettings = mqttSettings;
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();
        _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;
        _filechanged = filechanged;
        _onFileChanged = onFileChanged;
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
            Log.Information("Attempting to connect to MQTT broker at {Broker}:{Port}", _mqttSettings.Broker, _mqttSettings.Port);
            
            var connectResult = await _mqttClient.ConnectAsync(options);
            var payload = File.ReadAllBytes(_mqttSettings.PublishedFilePath);
            if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
            {
                Log.Information("Connected to MQTT broker as {Role}", _mqttSettings.Role);
                if (_onFileChanged == null)
                {
                    Log.Error("_onFileChanged is null. Initialize it before the loop.");
                    return; // or throw an exception based on your design choice
                }
                while (true)
                {
                    //_onFileChanged.InitializeFileWatcher(_mqttSettings.PublishedFilePath);
                    Log.Information("Inside publishing loop.");
                    await PublishAsync(_mqttSettings.Topic);
                    await Task.Delay(3000);
                }
            }
            else
            {
                Log.Error("Failed to connect to MQTT broker: {ResultCode}", connectResult.ResultCode);
            }
        }
        catch (MqttCommunicationException ex)
        {
            Log.Error("Communication error while connecting to MQTT broker: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error("Unexpected error during MQTT connection attempt: {Message}", ex.Message);
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

    private DateTime _lastFileModifiedTime;
    private string _lastFileContentsHash;

    public async Task PublishAsync(string topic, byte[]? payload = null)
    {
        await Task.Delay(3000); // Simulate some delay

        // If payload is not provided, attempt to get the database change payload
        Log.Information("payload is: {payload}", payload);
        if (payload == null)
        {
            payload = await _filechanged.DatabaseChangedAsync();
            // Proceed with publishing the payload
            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(mqttMessage);
            Log.Information("Published data to topic: {Topic}", topic);
            var payloadString = Encoding.UTF8.GetString(payload);
            Log.Information("Published data is {Payload}", payloadString);
        }
        else
        {
            if (!_mqttClient.IsConnected)
            {
                Log.Warning("MQTT client is not connected. Attempting to connect...");
                await TryConnectAsync();
            }

            // Define the CSV file path
            string csvFilePath = "C:\\Users\\studentinit\\Documents\\GitHub\\mqtt-test\\RoomTemperatures.csv";

            // Check if the CSV file has been modified
            var lastModifiedTime = System.IO.File.GetLastWriteTime(csvFilePath);

            // Check if the CSV file has been modified since last publish
            if (lastModifiedTime != _lastFileModifiedTime)
            {
                // File has been modified, read the contents
                var newFileContents = await System.IO.File.ReadAllTextAsync(csvFilePath);
                await Task.Delay(1000);

                Log.Information("New CSV File Contents: {Contents}", newFileContents);

                // Get the hash of the new file contents to detect changes
                var newFileContentsHash = GetHash(newFileContents);

                // Compare contents or hashes to determine if there's a change
                if (newFileContentsHash != _lastFileContentsHash)
                {
                    _lastFileModifiedTime = lastModifiedTime;
                    _lastFileContentsHash = newFileContentsHash;

                    // Log the change and prepare the payload for publishing
                    Log.Information("CSV file changed, preparing to publish.");

                    // If payload is still null after reading the CSV, use the CSV content
                    if (payload == null || payload.Length == 0)
                    {
                        payload = Encoding.UTF8.GetBytes(newFileContents);
                    }

                    // Proceed with publishing the payload
                    var mqttMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(topic)
                        .WithPayload(payload)
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                        .Build();

                    await _mqttClient.PublishAsync(mqttMessage);
                    Log.Information("Published data to topic: {Topic}", topic);
                    var payloadString = Encoding.UTF8.GetString(payload);
                    Log.Information("Published data is {Payload}", payloadString);
                }
                else
                {
                    Log.Information("No changes detected in the CSV file since last publish.");
                }
            }
            else
            {
                Log.Information("CSV file has not been modified since last publish.");
            }

            // If there's no data to publish from the file or the database
            if (payload == null || payload.Length == 0)
            {
                Log.Information("No data to publish.");
            }
        }
    }


    // Method to compute the hash of the file contents
    private string GetHash(string content)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }




    public async Task DisconnectAsync()
    {
        await _mqttClient.DisconnectAsync();
        Log.Information("Disconnected from MQTT broker");
    }
}
