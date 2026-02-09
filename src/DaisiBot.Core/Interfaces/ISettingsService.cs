using DaisiBot.Core.Models;

namespace DaisiBot.Core.Interfaces;

public interface ISettingsService
{
    Task<UserSettings> GetSettingsAsync();
    Task SaveSettingsAsync(UserSettings settings);
}
