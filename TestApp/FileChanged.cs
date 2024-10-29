using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestApp.MqttClientInterfaces;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static TestApp.MqttClientInterfaces.IFileChanged;

namespace TestApp
{
    public class FileChanged: IFileChanged
    {
        private readonly MqttSettings _mqttSettings;
        private readonly AppDbContext _appDbContext;

        public FileChanged(MqttSettings mqttSettings)
        {
            _mqttSettings = mqttSettings;
        }
        public async Task<byte[]> ReadPayloadAsync(object data)
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
            return payload;
        }


        public async Task<bool> FileChangedAsync (object data)
        {
            var fileChange = false;
            byte[] payload = await ReadPayloadAsync(data);
            byte[] lastPublishedPayload;


            lastPublishedPayload = await File.ReadAllBytesAsync(_mqttSettings.LocalFilePath);
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
         //Takes JSON of changes and converts it into array of bytes for the payload 
            DbChangeTracker dbUpdater = new DbChangeTracker(_appDbContext);
            string deltaJson = await dbUpdater.GenerateDeltaAsync(); 
            byte[] payload = Encoding.ASCII.GetBytes(deltaJson); 
            return payload;
        }

        public async Task<bool> DbChangeHappenedAsync()
        {
            var change = false;

            return change;
        }

       
    }
}
