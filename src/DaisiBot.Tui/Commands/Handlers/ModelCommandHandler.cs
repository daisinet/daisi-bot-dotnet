using DaisiBot.Core.Models;
using DaisiBot.Tui.Dialogs;

namespace DaisiBot.Tui.Commands.Handlers;

public class ModelCommandHandler(App app, IServiceProvider services)
{
    public Task<string?> HandleAsync(SlashCommand command)
    {
        app.Post(() =>
        {
            var flow = new ModelPickerFlow(app, services);
            app.RunModal(flow);
        });
        return Task.FromResult<string?>(null);
    }
}
