using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Npgsql;

public class DbUpdater
{
    private readonly string _connectionString;

    public DbUpdater(string connectionString)
    {
        _connectionString = connectionString;
    }

    // Main method to apply changes from JSON delta file to the database
    public async Task ApplyChangesAsync(string deltaJsonPath)
    {
        // Read and deserialize JSON changes
        string jsonContent = await File.ReadAllTextAsync(deltaJsonPath);
        var changes = JsonConvert.DeserializeObject<Delta>(jsonContent) ?? new Delta
        {
            Inserts = new List<Dictionary<string, object>>(),
            Updates = new List<Dictionary<string, object>>(),
            Deletes = new List<int>()
        };

        // Process inserts and updates
        foreach (var insert in changes.Inserts)
        {
            await InsertOrUpdateRowAsync(insert);
        }

        foreach (var update in changes.Updates)
        {
            await InsertOrUpdateRowAsync(update);
        }

        // Process deletes
        foreach (var deleteId in changes.Deletes)
        {
            await DeleteRowAsync(deleteId);
        }
    }

    // Method to read database changes with the `updated_flag`
    //public async Task<List<Dictionary<string, object>>> ReadDbChangesAsync()
    //{
    //    var changes = new List<Dictionary<string, object>>();
    //    using (var connection = new NpgsqlConnection(_connectionString))
    //    {
    //        await connection.OpenAsync();
    //        var query = @"SELECT * FROM public.""RoomTemperatures"" WHERE updated_flag = true";

    //        using (var command = new NpgsqlCommand(query, connection))
    //        using (var reader = await command.ExecuteReaderAsync())
    //        {
    //            while (await reader.ReadAsync())
    //            {
    //                var row = new Dictionary<string, object>();
    //                for (int i = 0; i < reader.FieldCount; i++)
    //                {
    //                    row[reader.GetName(i)] = reader.GetValue(i);
    //                }
    //                changes.Add(row);
    //            }
    //        }
    //    }
    //    return changes;
    //}

    // Helper method to insert or update rows based on primary key conflict
    private async Task InsertOrUpdateRowAsync(Dictionary<string, object> rowData)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var query = @"
            INSERT INTO public.""RoomTemperatures"" (""Id"", ""RoomName"", ""CurrentTemperature"", ""CurrentTime"", ""CreatedAt"", ""UpdatedAt"") 
            VALUES (@Id, @RoomName, @CurrentTemperature, @CurrentTime, @CreatedAt, @UpdatedAt) 
            ON CONFLICT (""Id"") 
            DO UPDATE SET 
                ""RoomName"" = EXCLUDED.""RoomName"", 
                ""CurrentTemperature"" = EXCLUDED.""CurrentTemperature"", 
                ""CurrentTime"" = EXCLUDED.""CurrentTime"", 
                ""UpdatedAt"" = EXCLUDED.""UpdatedAt""";

            using (var command = new NpgsqlCommand(query, connection))
            {
                // Assign parameters with null handling for missing or nullable values
                command.Parameters.AddWithValue("@Id", rowData["Id"]);
                command.Parameters.AddWithValue("@RoomName", rowData.ContainsKey("RoomName") ? rowData["RoomName"] : (object)DBNull.Value);
                command.Parameters.AddWithValue("@CurrentTemperature", rowData.ContainsKey("CurrentTemperature") ? rowData["CurrentTemperature"] : (object)DBNull.Value);
                command.Parameters.AddWithValue("@CurrentTime", rowData.ContainsKey("CurrentTime") ? rowData["CurrentTime"] : (object)DBNull.Value);
                command.Parameters.AddWithValue("@CreatedAt", rowData.ContainsKey("CreatedAt") ? rowData["CreatedAt"] : (object)DBNull.Value);
                command.Parameters.AddWithValue("@UpdatedAt", rowData.ContainsKey("UpdatedAt") ? rowData["UpdatedAt"] : (object)DBNull.Value);

                await command.ExecuteNonQueryAsync();
            }
        }
    }

    // Helper method to delete rows by Id
    private async Task DeleteRowAsync(int id)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var query = "DELETE FROM public.\"RoomTemperatures\" WHERE \"Id\" = @Id";

            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}

// Delta class to represent changes in JSON format
public class Delta
{
    public List<Dictionary<string, object>> Inserts { get; set; } = new List<Dictionary<string, object>>();
    public List<Dictionary<string, object>> Updates { get; set; } = new List<Dictionary<string, object>>();
    public List<int> Deletes { get; set; } = new List<int>();
}
