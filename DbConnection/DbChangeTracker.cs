using Newtonsoft.Json;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public class DbChangeTracker
{
    private readonly string _connectionString;
    private DateTime _lastSyncTime;

    public DbChangeTracker(string connectionString)
    {
        _connectionString = connectionString;
        // Initialize _lastSyncTime, possibly load it from persistent storage if needed
       
    }

    public async Task<string> GenerateDeltaAsync()
    {
        var changes = new
        {
            Inserts = await GetNewRowsAsync(),
            Updates = await GetUpdatedRowsAsync(),
            //Deletes = await GetDeletedRowsAsync()
        };

        // Update the last sync time after collecting changes
        _lastSyncTime = DateTime.UtcNow.AddMinutes(-1);

        return JsonConvert.SerializeObject(changes, Formatting.Indented);
    }

    public async Task SaveDeltaToFileAsync(string filePath)
    {
        var deltaJson = await GenerateDeltaAsync();
        await File.WriteAllTextAsync(filePath, deltaJson);

        // Optionally save _lastSyncTime to persistent storage here
    }

    private async Task<List<Dictionary<string, object>>> GetNewRowsAsync()
    {
        Console.WriteLine(_lastSyncTime);
        return await GetRowsAsync("SELECT * FROM public.\"RoomTemperatures\" WHERE \"CreatedAt\" > @lastSyncTime");
        
        

    }

    private async Task<List<Dictionary<string, object>>> GetUpdatedRowsAsync()
    {
        Console.WriteLine("Last sync time", _lastSyncTime);
        return await GetRowsAsync("SELECT * FROM public.\"RoomTemperatures\" WHERE \"UpdatedAt\" > @lastSyncTime");
        
    }

    //private async Task<List<int>> GetDeletedRowsAsync()
    //{
    //    var query = "SELECT id FROM deleted_items WHERE deleted_time > @lastSyncTime";
    //    var deletedIds = new List<int>();

    //    using (var connection = new NpgsqlConnection(_connectionString))
    //    {
    //        await connection.OpenAsync();
    //        using (var command = new NpgsqlCommand(query, connection))
    //        {
    //            command.Parameters.AddWithValue("@lastSyncTime", _lastSyncTime);

    //            using (var reader = await command.ExecuteReaderAsync())
    //            {
    //                while (await reader.ReadAsync())
    //                {
    //                    deletedIds.Add(reader.GetInt32(0));
    //                }
    //            }
    //        }
    //    }
    //    return deletedIds;
    //}


    private async Task<List<Dictionary<string, object>>> GetRowsAsync(string query)
    {
        var rows = new List<Dictionary<string, object>>();

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@lastSyncTime", _lastSyncTime);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object>();
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.GetValue(i);
                        }
                        rows.Add(row);
                    }
                }
            }
        }
        return rows;
    }
}
