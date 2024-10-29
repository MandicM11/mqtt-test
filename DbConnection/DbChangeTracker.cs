using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;

public class DbChangeTracker(AppDbContext context)
{
    private readonly AppDbContext _context = context;
    private static DateTime _lastSyncTime = DateTime.UtcNow.AddMinutes(-1).AddHours(1);

    public async Task<string> GenerateDeltaAsync()
    {
        var changes = new
        {
            Inserts = await GetNewRowsAsync(),
            Updates = await GetUpdatedRowsAsync(),
            // Deletes = await GetDeletedRowsAsync()
        };
        Log.Information("sync pre GenerateDelta je: {LT}", _lastSyncTime);
        _lastSyncTime = DateTime.UtcNow.AddHours(1);
        Log.Information("sync u GenerateDelta je: {LT}", _lastSyncTime);
        return JsonConvert.SerializeObject(changes, Formatting.Indented);
    }

    public async Task SaveDeltaToFileAsync( string filePath)
    {
        var deltaJson = await GenerateDeltaAsync();
        await File.WriteAllTextAsync(filePath, deltaJson);
        Log.Information("sync u GenerateDelta je: {LT}", _lastSyncTime);
    }

    private async Task<List<Dictionary<string, object>>> GetNewRowsAsync()
    {
        return await _context.RoomTemperatures
            .Where(r => r.CreatedAt > _lastSyncTime)
            .Select(r => new Dictionary<string, object>
            {
                { "Id", r.Id },
                { "RoomName", r.RoomName },
                { "CurrentTemperature", r.CurrentTemperature },
                { "CurrentTime", r.CurrentTime },
                { "CreatedAt", r.CreatedAt },
                { "UpdatedAt", r.UpdatedAt }
            })
            .ToListAsync();
    }

    private async Task<List<Dictionary<string, object>>> GetUpdatedRowsAsync()
    {
        return await _context.RoomTemperatures
            .Where(r => r.UpdatedAt > _lastSyncTime)
            .Select(r => new Dictionary<string, object>
            {
                { "Id", r.Id },
                { "RoomName", r.RoomName },
                { "CurrentTemperature", r.CurrentTemperature },
                { "CurrentTime", r.CurrentTime },
                { "CreatedAt", r.CreatedAt },
                { "UpdatedAt", r.UpdatedAt }
            })
            .ToListAsync();

    }
}
