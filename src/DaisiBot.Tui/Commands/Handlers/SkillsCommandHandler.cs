using DaisiBot.Core.Models;
using DaisiBot.Tui.Dialogs;

namespace DaisiBot.Tui.Commands.Handlers;

public class SkillsCommandHandler(App app, IServiceProvider services)
{
    public Task<string?> HandleAsync(SlashCommand command)
    {
        app.Post(() =>
        {
            var flow = new SkillBrowserFlow(app, services);
            app.RunModal(flow);
        });
        return Task.FromResult<string?>(null);
    }
}
