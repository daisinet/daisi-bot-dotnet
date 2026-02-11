using DaisiBot.Core.Models;

namespace DaisiBot.Tui.Commands.Handlers;

#pragma warning disable CS9113 // Parameter is unread
public class StatusCommandHandler(IServiceProvider services, SlashCommandDispatcher dispatcher)
#pragma warning restore CS9113
{
    public Task<string?> HandleAsync(SlashCommand command)
    {
        // Toggle the status panel visibility
        dispatcher.OnStatusToggle?.Invoke();
        return Task.FromResult<string?>(null);
    }
}
