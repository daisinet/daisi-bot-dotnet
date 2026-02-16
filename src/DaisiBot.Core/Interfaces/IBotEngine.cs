using DaisiBot.Core.Models;

namespace DaisiBot.Core.Interfaces;

public interface IBotEngine
{
    Task StartBotAsync(Guid botId);
    Task StopBotAsync(Guid botId);
    Task StopAllBotsAsync();
    Task RestartAllBotsAsync();
    Task SendInputAsync(Guid botId, string userInput);
    bool IsRunning(Guid botId);
    event EventHandler<BotInstance>? BotStatusChanged;
    event EventHandler<ActionPlanChangedEventArgs>? ActionPlanChanged;
    event EventHandler<BotLogEntry>? BotLogEntryAdded;

    /// <summary>
    /// Fired when the ORC returns a 401/Unauthenticated error, indicating
    /// the user needs to log in again.
    /// </summary>
    event EventHandler<Guid>? AuthenticationRequired;
}
