using DaisiBot.Core.Models;
using DaisiBot.Tui.Dialogs;

namespace DaisiBot.Tui.Commands.Handlers;

public class UpdateCommandHandler(App app, IServiceProvider services, SlashCommandDispatcher dispatcher)
{
    public Task<string?> HandleAsync(SlashCommand command)
    {
        var currentBot = dispatcher.CurrentBot;
        if (currentBot is null)
            return Task.FromResult<string?>("No bot selected. Select a bot first.");

        app.Post(() =>
        {
            var flow = new BotUpdateFlow(app, services, currentBot, () =>
            {
                dispatcher.OnBotUpdated?.Invoke();
            });
            app.RunModal(flow);
        });

        return Task.FromResult<string?>(null);
    }
}
