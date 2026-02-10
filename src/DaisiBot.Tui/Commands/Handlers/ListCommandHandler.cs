using System.Text;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Commands.Handlers;

public class ListCommandHandler(IServiceProvider services)
{
    public async Task<string?> HandleAsync(SlashCommand command)
    {
        var botStore = services.GetRequiredService<IBotStore>();
        var bots = await botStore.GetAllAsync();

        if (bots.Count == 0)
            return "No bots created. Use /new to create one.";

        var sb = new StringBuilder();
        sb.AppendLine($"{bots.Count} bot(s):");
        foreach (var bot in bots)
        {
            var icon = bot.Status switch
            {
                BotStatus.Running => "[Running]",
                BotStatus.Idle => "[Idle]",
                BotStatus.WaitingForInput => "[Waiting]",
                BotStatus.Completed => "[Done]",
                BotStatus.Failed => "[Failed]",
                BotStatus.Stopped => "[Stopped]",
                _ => "[?]"
            };
            sb.AppendLine($"  {icon} {bot.Label} - {bot.Goal[..Math.Min(40, bot.Goal.Length)]}");
        }
        return sb.ToString().TrimEnd();
    }
}
