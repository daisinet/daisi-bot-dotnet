using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Commands.Handlers;

public class StopCommandHandler(IServiceProvider services, SlashCommandDispatcher dispatcher)
{
    public async Task<string?> HandleAsync(SlashCommand command)
    {
        var currentBot = dispatcher.CurrentBot;
        if (currentBot is null)
            return "No bot selected. Select a bot first.";

        var botEngine = services.GetRequiredService<IBotEngine>();

        if (!botEngine.IsRunning(currentBot.Id))
            return $"Bot '{currentBot.Label}' is not running.";

        await botEngine.StopBotAsync(currentBot.Id);
        return $"Bot '{currentBot.Label}' stopped.";
    }
}
