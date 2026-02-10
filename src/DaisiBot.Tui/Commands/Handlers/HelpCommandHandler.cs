using DaisiBot.Core.Models;
using DaisiBot.Tui.Dialogs;

namespace DaisiBot.Tui.Commands.Handlers;

public class HelpCommandHandler(App app)
{
    public Task<string?> HandleAsync(SlashCommand command)
    {
        app.Post(() =>
        {
            var modal = new HelpModal(app);
            app.RunModal(modal);
        });
        return Task.FromResult<string?>(null);
    }
}
