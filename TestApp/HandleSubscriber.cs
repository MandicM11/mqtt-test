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
    public class HandleSubscriber
    {

        private readonly MqttSettings _mqttSettings;

        public HandleSubscriber(MqttSettings mqttSettings)
        {
            _mqttSettings = mqttSettings;
        }
        public async Task ReadFileAsync(byte[] payload)
        {
            var subscribeHelpers = new Helpers();
            

            string fileExtension = subscribeHelpers.GetFileExtension(payload);

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
                
                
                    await File.WriteAllBytesAsync(filePath, payload);
                    Log.Information("Received file and saved to: {FilePath}", filePath);
                
                  
                }
            
            else
            {
                Log.Warning("The SavedFilePath is invalid or empty.");
            }
        }
    }
}


//publisher has to read the file before he sends it and detect the change