using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using Serilog;
using Microsoft.Extensions.Configuration;


public class DbUpdater
{
    private readonly string _connectionString;

    public DbUpdater(string connectionString)
    {
        _connectionString = connectionString;
        

    }

    public async Task ApplyChangesAsync(string deltaJsonPath)
    {

       
        string ourJson = await File.ReadAllTextAsync(deltaJsonPath);
        var changes = JsonConvert.DeserializeObject<Delta>(ourJson);


        
        foreach (var insert in changes.Inserts)
        {
            await InsertOrUpdateRowAsync(insert);
            
        }

        
        foreach (var update in changes.Updates)
        {
            await InsertOrUpdateRowAsync(update);
        }


    //    foreach (var deleteId in changes.Deletes)
    //    {
    //        await DeleteRowAsync(deleteId);
    //    }
    }
    public async Task ReadDbChanges()
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var query = @"
            SELECT * FROM public.""RoomTemperatures"" WHERE updated_flag = true
            ";
        }
    }
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
               
                command.Parameters.AddWithValue("@Id", rowData["Id"]);
                command.Parameters.AddWithValue("@RoomName", rowData["RoomName"]);
                command.Parameters.AddWithValue("@CurrentTemperature", rowData["CurrentTemperature"]);
                command.Parameters.AddWithValue("@CurrentTime", rowData["CurrentTime"]);
                command.Parameters.AddWithValue("@CreatedAt", rowData["CreatedAt"]);
                command.Parameters.AddWithValue("@UpdatedAt", rowData["UpdatedAt"]);

                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task DeleteRowAsync(int id)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            var query = "DELETE FROM items WHERE Id = @Id";

            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}

public class Delta
{
    public List<Dictionary<string, object>> Inserts { get; set; }
    public List<Dictionary<string, object>> Updates { get; set; }
    public List<int> Deletes { get; set; }
}
