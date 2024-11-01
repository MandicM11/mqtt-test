using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;
using TestApp.MqttClientInterfaces;
using TestApp.helpers;

namespace TestApp
{
    public class OnFileChanged : IOnFileChanged
    {
        private readonly AppDbContext? _appDbContext; // Nullable AppDbContext
        private readonly MqttSettings _mqttSettings;
        private byte[]? _lastPayload;

        public OnFileChanged(MqttSettings mqttSettings, AppDbContext? appDbContext)
        {
            _mqttSettings = mqttSettings;
            _appDbContext = appDbContext; // Allow null assignment
            
        }

        public async Task ReadFileAsync(byte[] payload)
        {
            // Compare the new payload with the last known payload
            if (_lastPayload != null && _lastPayload.SequenceEqual(payload))
            {
                Log.Information("No change detected in the file. Skipping write.");
                return;
            }

            var subscribeHelpers = new Helpers();
            string fileExtension = subscribeHelpers.GetFileExtension(payload);
            Log.Information($"File extension detected: {fileExtension}");
            string projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
            var uniqueFileName = $"received_file{fileExtension}";
            var filePath = Path.Combine(projectDir, uniqueFileName);

            // Ensure the directory exists
            if (!string.IsNullOrWhiteSpace(_mqttSettings.SavedFilePath))
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Log.Information("Directory created: {Directory}", directory);
                }

                // Write the file to the specified path
                await File.WriteAllBytesAsync(filePath, payload);
                Log.Information("File saved at {FilePath}", filePath);

                // Update the last known payload
                _lastPayload = payload;
            }
            else
            {
                Log.Warning("The SavedFilePath is invalid or empty.");
            }
        }

        public async Task ReaderDatabaseAsync(byte[] payload)
        {
            // Only perform database operations if _appDbContext is not null
            if (_appDbContext != null)
            {
                DbUpdater dbUpdater = new DbUpdater(_appDbContext);
                try
                {
                    await dbUpdater.ApplyDbChangesAsync(_appDbContext, payload);
                    Log.Information("Database changes applied successfully.");
                }
                catch (Exception ex)
                {
                    Log.Error($"Error applying changes: {ex.Message}");
                }
            }
            else
            {
                Log.Warning("Database context is null. Skipping database operations.");
            }
        }
    }
}
