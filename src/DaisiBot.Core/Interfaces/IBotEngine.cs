using DaisiBot.Core.Models;

namespace DaisiBot.Core.Interfaces;

public interface IBotEngine
{
    Task StartBotAsync(Guid botId);
    Task StopBotAsync(Guid botId);
    Task SendInputAsync(Guid botId, string userInput);
    bool IsRunning(Guid botId);
    event EventHandler<BotInstance>? BotStatusChanged;
}
