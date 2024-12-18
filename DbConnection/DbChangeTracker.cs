﻿using Newtonsoft.Json;
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
    private static DateTime _lastSyncTime = DateTime.UtcNow.AddMinutes(-1);

    public async Task<string> GenerateDeltaAsync()
    {
        var changes = new
        {
            Inserts = await GetNewRowsAsync(),
            Updates = await GetUpdatedRowsAsync(),
            // Deletes = await GetDeletedRowsAsync()
        };
       
        return JsonConvert.SerializeObject(changes, Formatting.Indented);
    }


    public async Task SaveDeltaToFileAsync( string filePath)
    {
        var deltaJson = await GenerateDeltaAsync();
        await File.WriteAllTextAsync(filePath, deltaJson);
        ;
    }

    private async Task<List<Dictionary<string, object>>> GetNewRowsAsync()
    {
        // Retrieve new rows
        var newRows = await _context.RoomTemperatures
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

        // Update _lastSyncTime after successful retrieval
        if (newRows.Any())
        {
            _lastSyncTime = newRows.Max(r => (DateTime)r["CreatedAt"]);
        }

        return newRows;
    }

    private async Task<List<Dictionary<string, object>>> GetUpdatedRowsAsync()
    {
        // Retrieve updated rows
        var updatedRows = await _context.RoomTemperatures
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

        // Update _lastSyncTime after successful retrieval
        if (updatedRows.Any())
        {
            _lastSyncTime = updatedRows.Max(r => (DateTime)r["UpdatedAt"]);
        }

        return updatedRows;
    }

}
