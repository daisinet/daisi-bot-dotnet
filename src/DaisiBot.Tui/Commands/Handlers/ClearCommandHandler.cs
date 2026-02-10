using DaisiBot.Core.Models;

namespace DaisiBot.Tui.Commands.Handlers;

public class ClearCommandHandler
{
    public Task<string?> HandleAsync(SlashCommand command)
    {
        return Task.FromResult<string?>("__CLEAR__");
    }
}
