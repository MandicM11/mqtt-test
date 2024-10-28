using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

public class DbChangeTracker
{
    private readonly AppDbContext _context;
    private DateTime _lastSyncTime;

    public DbChangeTracker(AppDbContext context)
    {
        _context = context;
        _lastSyncTime = DateTime.UtcNow.AddMinutes(-1);
    }

    public async Task<string> GenerateDeltaAsync()
    {
        var changes = new
        {
            Inserts = await GetNewRowsAsync(),
            Updates = await GetUpdatedRowsAsync(),
            // Deletes = await GetDeletedRowsAsync()
        };

        _lastSyncTime = DateTime.UtcNow;

        return JsonConvert.SerializeObject(changes, Formatting.Indented);
    }

    public async Task SaveDeltaToFileAsync(string filePath)
    {
        var deltaJson = await GenerateDeltaAsync();
        await File.WriteAllTextAsync(filePath, deltaJson);
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
