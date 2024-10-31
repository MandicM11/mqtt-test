using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestApp.MqttClientInterfaces;
using TestApp.helpers;
using System.Security.Cryptography.X509Certificates;
using System.Formats.Asn1;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DbConnection;
using Microsoft.EntityFrameworkCore;


namespace TestApp
{
    public class OnFileChanged : IOnFileChanged
    {
        private readonly AppDbContext _appDbContext;
        private readonly MqttSettings _mqttSettings;
        private FileSystemWatcher _fileWatcher;
        private readonly FileChanged _filechanged;
        private readonly OnFileChanged _onFileChanged;


        public OnFileChanged(MqttSettings mqttSettings, AppDbContext appDbContext, FileChanged fileChanged)
        {
            _mqttSettings = mqttSettings;
            _appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
            _fileWatcher = new FileSystemWatcher();
            _filechanged = fileChanged;
            //_onFileChanged = onFileChanged;

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
        public async Task UpdateDatabaseFromCsvAsync(byte[] payload)
        {
            var csvContent = Encoding.UTF8.GetString(payload);

            using (var reader = new StringReader(csvContent))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var incomingRecords = csv.GetRecords<RoomTemperature>().ToList();
                Log.Information("Parsed {Count} records from CSV.", incomingRecords.Count);

                // Load existing records from the database
                var existingRecords = await _appDbContext.RoomTemperatures.ToListAsync();

                // Check for changes
                bool hasChanges = false;

                foreach (var incomingRecord in incomingRecords)
                {
                    // Ensure DateTime properties are in UTC
                    ConvertDateTimeToUtc(incomingRecord);

                    // Check if the record already exists and if it has changed
                    var existingRecord = existingRecords.FirstOrDefault(r => r.Id == incomingRecord.Id);

                    if (existingRecord != null)
                    {
                        // Compare fields to determine if there are changes
                        if (!AreRecordsEqual(existingRecord, incomingRecord))
                        {
                            Log.Information("Updating existing record with Id: {Id}", incomingRecord.Id);
                            // Update fields here
                            UpdateExistingRecord(existingRecord, incomingRecord);
                            hasChanges = true;
                        }
                        else
                        {
                            Log.Information("No changes detected for record with Id: {Id}", incomingRecord.Id);
                        }
                    }
                    else
                    {
                        Log.Information("Adding new record with Id: {Id}", incomingRecord.Id);
                        _appDbContext.RoomTemperatures.Add(incomingRecord);
                        hasChanges = true;
                    }
                }

                if (hasChanges)
                {
                    try
                    {
                        Log.Information("Attempting to save records to the database.");
                        int savedRecordsCount = await _appDbContext.SaveChangesAsync();

                        if (savedRecordsCount > 0)
                        {
                            Log.Information("Successfully updated {Count} records in the database.", savedRecordsCount);
                        }
                        else
                        {
                            Log.Information("No records were updated in the database.");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Logs detailed inner exception message if present
                        Log.Error("Failed to save records: {Message} - Inner Exception: {InnerException}", ex.Message, ex.InnerException?.Message);
                    }
                }
                else
                {
                    Log.Information("No changes detected in the CSV file. Skipping database update.");
                }
            }
        }

        // Method to convert DateTime properties to UTC
        private void ConvertDateTimeToUtc(RoomTemperature record)
        {
            if (record.CurrentTime.HasValue && record.CurrentTime.Value.Kind != DateTimeKind.Utc)
            {
                record.CurrentTime = TimeZoneInfo.ConvertTimeToUtc(record.CurrentTime.Value);
            }
            if (record.UpdatedAt.HasValue && record.UpdatedAt.Value.Kind != DateTimeKind.Utc)
            {
                record.UpdatedAt = TimeZoneInfo.ConvertTimeToUtc(record.UpdatedAt.Value);
            }
            if (record.CreatedAt.HasValue && record.CreatedAt.Value.Kind != DateTimeKind.Utc)
            {
                record.CreatedAt = TimeZoneInfo.ConvertTimeToUtc(record.CreatedAt.Value);
            }
        }

        // Method to compare two RoomTemperature records for equality
        private bool AreRecordsEqual(RoomTemperature existingRecord, RoomTemperature incomingRecord)
        {
            return existingRecord.CurrentTime == incomingRecord.CurrentTime &&
                   existingRecord.UpdatedAt == incomingRecord.UpdatedAt &&
                   existingRecord.CreatedAt == incomingRecord.CreatedAt &&
                   existingRecord.CurrentTemperature == incomingRecord.CurrentTemperature;
            // Add other fields as necessary for comparison
        }

        // Method to update an existing record
        private void UpdateExistingRecord(RoomTemperature existingRecord, RoomTemperature incomingRecord)
        {
            existingRecord.CurrentTemperature = incomingRecord.CurrentTemperature;
            existingRecord.CurrentTime = incomingRecord.CurrentTime;
            existingRecord.UpdatedAt = incomingRecord.UpdatedAt;
            existingRecord.CreatedAt = incomingRecord.CreatedAt;
            // Add any additional field updates here
        }


        public void InitializeFileWatcher(string csvFilePath)
        {
            _fileWatcher = new FileSystemWatcher
            {
            Path = Path.GetDirectoryName(csvFilePath),
            Filter = Path.GetFileName(csvFilePath),
            NotifyFilter = NotifyFilters.LastWrite
        };

            _fileWatcher.Changed += OnCsvFileChanged;
            _fileWatcher.EnableRaisingEvents = true;
        }

        public async void OnCsvFileChanged(object sender, FileSystemEventArgs e)
        {
            // Assuming mqttSettings is accessible here
            PublisherClientService publishService = new PublisherClientService(_mqttSettings,_filechanged,_onFileChanged);

            Log.Information("CSV file changed: {FilePath}", e.FullPath);

            // Read the updated CSV file
            var payload = await ReadCsvFileAsync(e.FullPath);

            if (payload != null && payload.Length > 0)
            {
                // Call the publish method
                await publishService.PublishAsync(_mqttSettings.Topic);
            }
            else
            {
                Log.Information("No data to publish from changed file.");
            }
        }

        // Read the CSV file and return its contents as a byte array
        private async Task<byte[]> ReadCsvFileAsync(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
    }
}


//publisher has to read the file before he sends it and detect the change