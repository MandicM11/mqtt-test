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
                //{
                //    // Publish either a message or a file
                //    if (_mqttSettings.PublishFilePath != null)
                //    {
                //        var fileBytes = await File.ReadAllBytesAsync(_mqttSettings.PublishFilePath);
                //        await PublishAsync(_mqttSettings.Topic, fileBytes);  
                //    }
                //    else
                //    {
                //        await PublishAsync(_mqttSettings.Topic, "Hello!"); 
                //    }
                //}

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
        byte[] payload;

        if (data is string messageOrFilePath)
        {
            if (File.Exists(messageOrFilePath))  // Check if it's a file path
            {
                // Read file content into byte array
                payload = await File.ReadAllBytesAsync(messageOrFilePath);
            }
            else 
            {
                // Treat it as a regular message if it's not a file path
                payload = Encoding.UTF8.GetBytes(messageOrFilePath);
            }
        }
       
        else
        {
            throw new ArgumentException("Unsupported data type for publishing.");
        }

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

            // Determine whether the payload is a string or a file
            if (IsTextPayload(payload))
            {
                // If it's a string, decode it
                var message = Encoding.UTF8.GetString(payload);
                Log.Information("Received message: {Message}", message);
            }
            else
            {
                // If it's binary (assumed to be a file), save it
                try
                {
                    string fileExtension = GetFileExtension(payload);
                    Log.Information($"{fileExtension}");
                    var uniqueFileName = $"received_file{fileExtension}";
                    var filePath = Path.Combine(_mqttSettings.SavedFilePath, uniqueFileName);

                    // Ensure the directory exists
                    if (!string.IsNullOrWhiteSpace(_mqttSettings.SavedFilePath))
                    {
                        var directory = Path.GetDirectoryName(filePath);
                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                            Log.Information("Directory created: {Directory}", directory);
                        }

                        // Write the received payload to a new file
                        await File.WriteAllBytesAsync(filePath, payload);
                        Log.Information("Received file and saved to: {FilePath}", filePath);
                    }
                    else
                    {
                        Log.Error("Saved file path is null or empty.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Error processing received message: {Message}", ex.Message);
                }
            }
        };
    }


    private string GetFileExtension(byte[] payload)
    {
        // Simple checks to determine the file type based on magic numbers
        if (payload.Length >= 4)
        {
            // Check for common image file signatures (magic numbers)
            if (payload[0] == 0xFF && payload[1] == 0xD8) // JPEG
            {
                return ".jpg";
            }
            else if (payload[0] == 0x89 && payload[1] == 0x50 && payload[2] == 0x4E && payload[3] == 0x47) // PNG
            {
                return ".png";
            }
            else if (payload[0] == 0x47 && payload[1] == 0x49 && payload[2] == 0x46) // GIF
            {
                return ".gif";
            }
            // Add more checks for other file types as needed
        }

        // Default to .bin if file type is not recognized
        return ".bin";
    }


    // Helper method to determine if the payload is text or binary
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
