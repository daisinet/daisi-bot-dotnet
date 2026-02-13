using DaisiBot.Core.Models;

namespace DaisiBot.Core.Interfaces;

public interface IBotStore
{
    Task<List<BotInstance>> GetAllAsync();
    Task<BotInstance?> GetAsync(Guid id);
    Task<BotInstance> CreateAsync(BotInstance bot);
    Task UpdateAsync(BotInstance bot);
    Task DeleteAsync(Guid id);
    Task<List<BotInstance>> GetRunnableAsync();
    Task AddLogEntryAsync(BotLogEntry entry);
    Task<List<BotLogEntry>> GetLogEntriesAsync(Guid botId, int limit = 200);
    Task ClearLogAsync(Guid botId);
    Task<List<BotStep>> GetStepsAsync(Guid botId);
    Task SetStepsAsync(Guid botId, List<BotStep> steps);
    Task AddMemoryAsync(BotMemoryEntry entry);
    Task<List<BotMemoryEntry>> GetMemoriesAsync(Guid botId, int limit = 50);
    Task ClearMemoryAsync(Guid botId);
    Task PruneMemoryAsync(Guid botId, int maxEntries);
}
