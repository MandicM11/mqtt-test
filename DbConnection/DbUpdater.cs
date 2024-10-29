using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DbConnection;
using Serilog;

public class DbUpdater(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task ApplyChangesAsync(AppDbContext context, string deltaJsonPath)
    {
        string jsonContent = await File.ReadAllTextAsync(deltaJsonPath);
        var changes = JsonConvert.DeserializeObject<Delta>(jsonContent) ?? new Delta();

        foreach (var insert in changes.Inserts)
        {
            await InsertRowAsync(context, insert);
        }

        foreach (var update in changes.Updates)
        {
            await UpdateRowAsync(context, update);
        }

        foreach (var deleteId in changes.Deletes)
        {
            await DeleteRowAsync(deleteId);
        }
        
        await context.SaveChangesAsync();
        
    }

    private async Task InsertRowAsync(AppDbContext context,Dictionary<string, object> rowData)
    {
        int id = Convert.ToInt32(rowData["Id"]);

        var roomTemp = new RoomTemperature
        {
            Id = id,
            RoomName = rowData.ContainsKey("RoomName") ? (string)rowData["RoomName"] : null,
            CurrentTemperature = rowData.ContainsKey("CurrentTemperature") && rowData["CurrentTemperature"] != null
                                ? (double?)Convert.ToDouble(rowData["CurrentTemperature"])
                                : null,
            CurrentTime = rowData.ContainsKey("CurrentTime") && rowData["CurrentTime"] != null
                ? (DateTime)rowData["CurrentTime"]
                : DateTime.UtcNow,

            CreatedAt = rowData.ContainsKey("CreatedAt") && rowData["CreatedAt"] != null
                ? (DateTime)rowData["CreatedAt"]
                : DateTime.UtcNow,

            UpdatedAt = rowData.ContainsKey("UpdatedAt") && rowData["UpdatedAt"] != null
                ? (DateTime)rowData["UpdatedAt"]
                : DateTime.UtcNow,

        };  

        ;
        await context.RoomTemperatures.AddAsync(roomTemp);
        
    }

    public async Task UpdateRowAsync(AppDbContext context, Dictionary<string, object> rowData)
    {
        int id = Convert.ToInt32(rowData["Id"]);
        Log.Information("context u update je: {Context}", context);
        var roomTemp = await context.RoomTemperatures.FindAsync(id);

        if (roomTemp != null)
        {
            // Update fields conditionally based on the existence of the key in rowData
            roomTemp.RoomName = rowData.ContainsKey("RoomName") ? (string)rowData["RoomName"] : roomTemp.RoomName;

            if (rowData.ContainsKey("CurrentTemperature") && rowData["CurrentTemperature"] != null)
            {
                roomTemp.CurrentTemperature = Convert.ToDouble(rowData["CurrentTemperature"]);
            }

            // For CurrentTime
            if (rowData.ContainsKey("CurrentTime") && rowData["CurrentTime"] is string currentTimeStr)
            {
                roomTemp.CurrentTime = DateTime.Parse(currentTimeStr);
            }

            // For CreatedAt
            if (rowData.ContainsKey("CreatedAt") && rowData["CreatedAt"] is string createdAtStr)
            {
                roomTemp.CreatedAt = DateTime.Parse("CreatedAt");
            }

            // For UpdatedAt
            if (rowData.ContainsKey("UpdatedAt") && rowData["UpdatedAt"] is string updatedAtStr)
            {
                roomTemp.UpdatedAt = DateTime.Parse(updatedAtStr);
            }

            context.RoomTemperatures.Update(roomTemp);
        }
    }


    private async Task DeleteRowAsync(int id)
    {
        var entityToDelete = await _context.RoomTemperatures.FindAsync(id);
        if (entityToDelete != null)
        {
            _context.RoomTemperatures.Remove(entityToDelete);
        }
    }
}

public class Delta
{
    public List<Dictionary<string, object>> Inserts { get; set; } = new List<Dictionary<string, object>>();
    public List<Dictionary<string, object>> Updates { get; set; } = new List<Dictionary<string, object>>();
    public List<int> Deletes { get; set; } = new List<int>();
}
