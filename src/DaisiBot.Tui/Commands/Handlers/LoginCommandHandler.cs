using DaisiBot.Core.Models;
using DaisiBot.Tui.Dialogs;

namespace DaisiBot.Tui.Commands.Handlers;

public class LoginCommandHandler(App app, IServiceProvider services)
{
    public Task<string?> HandleAsync(SlashCommand command)
    {
        app.Post(() =>
        {
            var flow = new LoginFlow(app, services);
            app.RunModal(flow);
        });
        return Task.FromResult<string?>(null);
    }
}
