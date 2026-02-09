using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DaisiBot.Data.Stores;

public class SqliteSettingsService(IDbContextFactory<DaisiBotDbContext> dbFactory) : ISettingsService
{
    public async Task<UserSettings> GetSettingsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.FindAsync(1);
        if (settings is null)
        {
            settings = new UserSettings();
            db.Settings.Add(settings);
            await db.SaveChangesAsync();
        }
        return settings;
    }

    public async Task SaveSettingsAsync(UserSettings settings)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.Settings.FindAsync(settings.Id);
        if (existing is null)
        {
            db.Settings.Add(settings);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(settings);
        }
        await db.SaveChangesAsync();
    }
}
