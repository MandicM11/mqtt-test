using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet;
using Serilog;
using System.Text;
using TestApp.MqttClientInterfaces;
using TestApp;

public class PublisherClientService : IPublisher
{
    private readonly MqttSettings _mqttSettings;
    private readonly IMqttClient _mqttClient;
    private readonly FileChanged _filechanged;
    private bool _isConnecting;
    private readonly SemaphoreSlim _reconnectSemaphore = new SemaphoreSlim(1, 1);
    private bool _isPublishing;
    private DateTime _lastFileModifiedTime;
    private string _lastFileContentsHash;

    public PublisherClientService(MqttSettings mqttSettings, FileChanged filechanged)
    {
        _mqttSettings = mqttSettings;
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();
        _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;
        _filechanged = filechanged;
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

            if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
            {
                Log.Information("Connected to MQTT broker as {Role}", _mqttSettings.Role);
                InitializeFileWatcher(_mqttSettings.PublishedFilePath); // Initialize file watcher upon connection
            }
            else
            {
                Log.Error("Failed to connect to MQTT broker: {ResultCode}", connectResult.ResultCode);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Error while connecting to MQTT broker: {Message}", ex.Message);
        }
    }

    private void InitializeFileWatcher(string filePath)
    {
        var watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath))
        {
            Filter = Path.GetFileName(filePath),
            NotifyFilter = NotifyFilters.LastWrite
        };
        watcher.Changed += async (sender, args) =>
        {
            if (!_isPublishing) await PublishFileChangeAsync();
        };
        watcher.EnableRaisingEvents = true;
    }

    private async Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        Log.Warning("Disconnected from MQTT broker. Attempting to reconnect...");
        if (_reconnectSemaphore.CurrentCount == 0) return;

        await _reconnectSemaphore.WaitAsync();
        try { await ReconnectAsync(); }
        finally { _reconnectSemaphore.Release(); }
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
                await Task.Delay(3000);
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

    public async Task PublishFileChangeAsync()
    {
        _isPublishing = true;
        try
        {
            var filePath = _mqttSettings.PublishedFilePath;
            var lastModifiedTime = File.GetLastWriteTime(filePath);
            if (lastModifiedTime == _lastFileModifiedTime) return;

            var newFileContents = await ReadFileWithRetryAsync(filePath);
            var newFileContentsHash = GetHash(newFileContents);

            if (newFileContentsHash != _lastFileContentsHash)
            {
                _lastFileModifiedTime = lastModifiedTime;
                _lastFileContentsHash = newFileContentsHash;

                var payload = Encoding.UTF8.GetBytes(newFileContents);
                var mqttMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(_mqttSettings.Topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await _mqttClient.PublishAsync(mqttMessage);
                Log.Information("Published updated file content to topic: {Topic}", _mqttSettings.Topic);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Error publishing file change: {Message}", ex.Message);
        }
        finally
        {
            _isPublishing = false;
        }
    }

    private string GetHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        return Convert.ToBase64String(sha256.ComputeHash(bytes));
    }

    public async Task DisconnectAsync()
    {
        await _mqttClient.DisconnectAsync();
        Log.Information("Disconnected from MQTT broker");
    }

    private async Task<string?> ReadFileWithRetryAsync(string filePath, int retryCount = 5, int delayMilliseconds = 200)
    {
        for (int i = 0; i < retryCount; i++)
        {
            try
            {
                // Attempt to read the file content
                return await File.ReadAllTextAsync(filePath);
            }
            catch (IOException ex) when (ex.Message.Contains("being used by another process"))
            {
                Log.Warning("File is currently in use, retrying... Attempt {Attempt}", i + 1);
                await Task.Delay(delayMilliseconds); // Wait briefly before retrying
            }
            catch (Exception ex)
            {
                Log.Error("Unexpected error while reading the file: {Message}", ex.Message);
                break; // Exit if an unexpected error occurs
            }
        }

        Log.Error("Unable to access the file after {RetryCount} attempts.", retryCount);
        return null;
    }

}
