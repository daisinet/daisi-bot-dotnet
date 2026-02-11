using DaisiBot.Core.Models;
using DaisiBot.Tui.Dialogs;

namespace DaisiBot.Tui.Commands.Handlers;

public class BalanceCommandHandler(App app, IServiceProvider services)
{
    public Task<string?> HandleAsync(SlashCommand command)
    {
        app.Post(() =>
        {
            var dialog = new BalanceDialog(app, services);
            app.RunModal(dialog);
        });
        return Task.FromResult<string?>(null);
    }
}
