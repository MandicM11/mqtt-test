﻿using MQTTnet;
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


public class PublisherClientService : IPublisher
{
    private readonly MqttSettings _mqttSettings;
    private readonly IMqttClient _mqttClient;
    private readonly FileChanged _filechanged;
    private bool _isConnecting;
    private readonly SemaphoreSlim _reconnectSemaphore = new SemaphoreSlim(1, 1);

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
                while (true)
                {
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

    // Publisher Implementation 
    public async Task PublishAsync(string topic)
    {
        Log.Information("ovde sam usao6");
        //byte[] payload = await _filechanged.ReadPayloadAsync(data);
        //bool fileChange = await _filechanged.FileChangedAsync(data);
        //Log.Information("fileChange: {FileChange}", fileChange);
        byte[] payload = await _filechanged.DatabaseChangedAsync();
        //bool dbChange = await _filechanged.DbChangeHappenedAsync(flag);
        if(payload != null || payload.Length != 0)
        {
            Log.Information("We have a change so we are publishing");
            var mqttMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

            await _mqttClient.PublishAsync(mqttMessage);
            //await File.WriteAllBytesAsync(_mqttSettings.LocalFilePath, payload);
            Log.Information("Published data to topic: {Topic}", topic);


        }

    }
   
    public async Task DisconnectAsync()
    {
        await _mqttClient.DisconnectAsync();
        Log.Information("Disconnected from MQTT broker");
    }
}
