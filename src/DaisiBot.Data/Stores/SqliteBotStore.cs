using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DaisiBot.Data.Stores;

public class SqliteBotStore(IDbContextFactory<DaisiBotDbContext> dbFactory) : IBotStore
{
    public async Task<List<BotInstance>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Bots
            .OrderByDescending(b => b.UpdatedAt)
            .ToListAsync();
    }

    public async Task<BotInstance?> GetAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Bots.FindAsync(id);
    }

    public async Task<BotInstance> CreateAsync(BotInstance bot)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Bots.Add(bot);
        await db.SaveChangesAsync();
        return bot;
    }

    public async Task UpdateAsync(BotInstance bot)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        bot.UpdatedAt = DateTime.UtcNow;
        db.Bots.Update(bot);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var bot = await db.Bots.FindAsync(id);
        if (bot is not null)
        {
            var memories = await db.BotMemoryEntries.Where(m => m.BotId == id).ToListAsync();
            db.BotMemoryEntries.RemoveRange(memories);
            db.Bots.Remove(bot);
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<BotInstance>> GetRunnableAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        return await db.Bots
            .Where(b => b.Status == BotStatus.Running && (b.NextRunAt == null || b.NextRunAt <= now))
            .ToListAsync();
    }

    public async Task AddLogEntryAsync(BotLogEntry entry)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.BotLogEntries.Add(entry);
        await db.SaveChangesAsync();
    }

    public async Task<List<BotLogEntry>> GetLogEntriesAsync(Guid botId, int limit = 200)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.BotLogEntries
            .Where(e => e.BotId == botId)
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();
    }

    public async Task ClearLogAsync(Guid botId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var entries = await db.BotLogEntries
            .Where(e => e.BotId == botId)
            .ToListAsync();
        db.BotLogEntries.RemoveRange(entries);
        await db.SaveChangesAsync();
    }

    public async Task<List<BotStep>> GetStepsAsync(Guid botId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.BotSteps
            .Where(s => s.BotId == botId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync();
    }

    public async Task SetStepsAsync(Guid botId, List<BotStep> steps)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.BotSteps
            .Where(s => s.BotId == botId)
            .ToListAsync();
        db.BotSteps.RemoveRange(existing);

        foreach (var step in steps)
        {
            step.BotId = botId;
            db.BotSteps.Add(step);
        }

        await db.SaveChangesAsync();
    }

    public async Task AddMemoryAsync(BotMemoryEntry entry)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.BotMemoryEntries.Add(entry);
        await db.SaveChangesAsync();
    }

    public async Task<List<BotMemoryEntry>> GetMemoriesAsync(Guid botId, int limit = 50)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.BotMemoryEntries
            .Where(m => m.BotId == botId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task ClearMemoryAsync(Guid botId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var entries = await db.BotMemoryEntries
            .Where(m => m.BotId == botId)
            .ToListAsync();
        db.BotMemoryEntries.RemoveRange(entries);
        await db.SaveChangesAsync();
    }

    public async Task PruneMemoryAsync(Guid botId, int maxEntries)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var count = await db.BotMemoryEntries.CountAsync(m => m.BotId == botId);
        if (count <= maxEntries) return;

        var toRemove = await db.BotMemoryEntries
            .Where(m => m.BotId == botId)
            .OrderBy(m => m.CreatedAt)
            .Take(count - maxEntries)
            .ToListAsync();
        db.BotMemoryEntries.RemoveRange(toRemove);
        await db.SaveChangesAsync();
    }
}
