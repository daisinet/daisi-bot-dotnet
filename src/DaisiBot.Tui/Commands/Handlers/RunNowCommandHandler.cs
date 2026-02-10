using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Commands.Handlers;

public class RunNowCommandHandler(IServiceProvider services, SlashCommandDispatcher dispatcher)
{
    public async Task<string?> HandleAsync(SlashCommand command)
    {
        var currentBot = dispatcher.CurrentBot;
        if (currentBot is null)
            return "No bot selected. Select a bot first.";

        var botEngine = services.GetRequiredService<IBotEngine>();

        if (botEngine.IsRunning(currentBot.Id))
            return $"Bot '{currentBot.Label}' is already running.";

        await botEngine.StartBotAsync(currentBot.Id);
        return $"Bot '{currentBot.Label}' started. Schedule unchanged.";
    }
}
