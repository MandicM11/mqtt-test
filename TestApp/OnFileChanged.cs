using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestApp.MqttClientInterfaces;
using TestApp.helpers;
using System.Security.Cryptography.X509Certificates;


namespace TestApp
{
    public class OnFileChanged : IOnFileChanged
    {
        private readonly AppDbContext _appDbContext;
        private readonly MqttSettings _mqttSettings;

        public OnFileChanged(MqttSettings mqttSettings, AppDbContext appDbContext)
        {
            _mqttSettings = mqttSettings;
            _appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
        }
        public async Task ReadFileAsync(byte[] payload)
        {
            var subscribeHelpers = new Helpers();
            DbUpdater dbUpdater = new DbUpdater(_appDbContext);

            string fileExtension = subscribeHelpers.GetFileExtension(payload);
            Log.Information($"File extension detected: {fileExtension}");

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

                // Write the file to the specified path
                await File.WriteAllBytesAsync(filePath, payload);
                Log.Information("File saved at {FilePath}", filePath);
            }
            else
            {
                Log.Warning("The SavedFilePath is invalid or empty.");
            }
        }

        public async Task ReaderDatabaseAsync(byte[] payload)
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

    }
}


//publisher has to read the file before he sends it and detect the change