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
}
