using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using DaisiBot.Tui.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Commands.Handlers;

public class KillCommandHandler(App app, IServiceProvider services, SlashCommandDispatcher dispatcher)
{
    public Task<string?> HandleAsync(SlashCommand command)
    {
        var botEngine = services.GetRequiredService<IBotEngine>();
        var botStore = services.GetRequiredService<IBotStore>();

        // If a specific bot label is given, use that
        if (command.Args.Length > 0)
        {
            var label = command.RawArgs;
            return KillByLabelAsync(label, botEngine, botStore);
        }

        // Otherwise use the currently selected bot
        var bot = dispatcher.CurrentBot;
        if (bot is null)
            return Task.FromResult<string?>("No bot selected. Select a bot first or use /kill <label>.");

        // Show confirmation dialog on UI thread
        app.Post(() =>
        {
            var dialog = new ConfirmDialog(app,
                $"Kill and delete '{bot.Label}'?",
                confirmed =>
                {
                    if (!confirmed) return;

                    Task.Run(async () =>
                    {
                        if (botEngine.IsRunning(bot.Id))
                            await botEngine.StopBotAsync(bot.Id);
                        await botStore.DeleteAsync(bot.Id);

                        app.Post(() =>
                        {
                            dispatcher.CurrentBot = null;
                            dispatcher.OnBotDeleted?.Invoke();
                        });
                    });
                });
            app.RunModal(dialog);
        });

        // Return null — the result will be shown via the callback
        return Task.FromResult<string?>(null);
    }

    private static async Task<string?> KillByLabelAsync(string label, IBotEngine botEngine, IBotStore botStore)
    {
        var allBots = await botStore.GetAllAsync();
        var match = allBots.Find(b =>
            b.Label.Equals(label, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            return $"No bot found with label '{label}'.";

        if (botEngine.IsRunning(match.Id))
            await botEngine.StopBotAsync(match.Id);
        return $"Stopped bot: {match.Label}";
    }
}
