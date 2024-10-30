using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TestApp.MqttClientInterfaces;

namespace TestApp
{
    public class FileChanged : IFileChanged
    {
        private readonly MqttSettings _mqttSettings;
        private readonly AppDbContext _appDbContext;

        // Add AppDbContext as a constructor parameter
        public FileChanged(MqttSettings mqttSettings, AppDbContext appDbContext)
        {
            _mqttSettings = mqttSettings;
            _appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
        }

        public async Task<byte[]> ReadPayloadAsync(object data)
        {
            byte[] payload;

            if (data is string messageOrFilePath)
            {
                if (File.Exists(messageOrFilePath))
                {
                    payload = await File.ReadAllBytesAsync(messageOrFilePath);
                }
                else
                {
                    payload = Encoding.UTF8.GetBytes(messageOrFilePath);
                }
            }
            else
            {
                throw new ArgumentException("Unsupported data type for publishing.");
            }
            return payload;
        }

        public async Task<bool> FileChangedAsync(object data)
        {
            var fileChange = false;
            byte[] payload = await ReadPayloadAsync(data);
            byte[] lastPublishedPayload = await File.ReadAllBytesAsync(_mqttSettings.LocalFilePath);

            fileChange = await compareFilesAsync(payload, lastPublishedPayload);
            return fileChange;
        }

        public async Task<bool> compareFilesAsync(byte[] payload, byte[] lastPublishedPayload)
        {
            var fileChanged = false;
            try
            {
                bool filesAreEqual = lastPublishedPayload.SequenceEqual(payload);
                if (!filesAreEqual)
                {
                    fileChanged = true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("An error occurred while comparing files: {Message}", ex.Message);
            }
            return fileChanged;
        }

        public async Task<byte[]> DatabaseChangedAsync()
        {
            DbChangeTracker dbUpdater = new DbChangeTracker(_appDbContext);
            string deltaJson = await dbUpdater.GenerateDeltaAsync();

            // Parse JSON and check if both "Inserts" and "Updates" are empty
            var jsonDoc = JsonDocument.Parse(deltaJson);
            bool hasInserts = jsonDoc.RootElement.GetProperty("Inserts").GetArrayLength() > 0;
            bool hasUpdates = jsonDoc.RootElement.GetProperty("Updates").GetArrayLength() > 0;

            // If no inserts or updates, return an empty byte array
            if (!hasInserts && !hasUpdates)
            {
                Log.Information("No changes detected. Skipping publish.");
                return Array.Empty<byte>();
            }

            Log.Information("Changes detected. Preparing payload for publishing.");
            byte[] payload = Encoding.ASCII.GetBytes(deltaJson);
            return payload;
        }

       
    }
}
