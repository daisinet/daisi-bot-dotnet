using System.Text;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Commands.Handlers;

public class StatusCommandHandler(IServiceProvider services, SlashCommandDispatcher dispatcher)
{
    public async Task<string?> HandleAsync(SlashCommand command)
    {
        var botStore = services.GetRequiredService<IBotStore>();
        var botEngine = services.GetRequiredService<IBotEngine>();

        // If a current bot is selected, show its status
        var currentBot = dispatcher.CurrentBot;
        if (currentBot is not null)
        {
            // Reload fresh state from store
            var fresh = await botStore.GetAsync(currentBot.Id);
            if (fresh is null)
                return $"Bot '{currentBot.Label}' no longer exists.";

            return FormatBotStatus(fresh, botEngine);
        }

        // Fallback: show all bots
        var bots = await botStore.GetAllAsync();
        if (bots.Count == 0)
            return "No bots. Use /new to create one.";

        var sb = new StringBuilder();
        foreach (var bot in bots)
        {
            sb.AppendLine(FormatBotStatus(bot, botEngine));
        }
        return sb.ToString().TrimEnd();
    }

    private static string FormatBotStatus(BotInstance bot, IBotEngine botEngine)
    {
        var running = botEngine.IsRunning(bot.Id) ? " [ACTIVE]" : "";
        var sb = new StringBuilder();
        sb.AppendLine($"{bot.Label}: {bot.Status}{running}");
        sb.AppendLine($"  Goal: {bot.Goal}");
        sb.AppendLine($"  Schedule: {bot.ScheduleType}");
        sb.AppendLine($"  Runs: {bot.ExecutionCount}");
        if (bot.LastRunAt.HasValue)
            sb.AppendLine($"  Last run: {bot.LastRunAt.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        if (bot.NextRunAt.HasValue)
            sb.AppendLine($"  Next run: {bot.NextRunAt.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        if (bot.PendingQuestion is not null)
            sb.AppendLine($"  Pending: {bot.PendingQuestion}");
        if (bot.LastError is not null)
            sb.AppendLine($"  Error: {bot.LastError}");
        return sb.ToString().TrimEnd();
    }
}
