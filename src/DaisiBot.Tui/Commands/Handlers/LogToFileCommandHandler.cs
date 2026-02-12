using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Commands.Handlers;

public class LogToFileCommandHandler(IServiceProvider services)
{
    public async Task<string?> HandleAsync(SlashCommand command)
    {
        var settingsService = services.GetRequiredService<ISettingsService>();
        var settings = await settingsService.GetSettingsAsync();

        if (command.Args.Length == 0)
            return $"File logging is {(settings.BotFileLoggingEnabled ? "on" : "off")}. Usage: /log-to-file <on|off>";

        var arg = command.Args[0].ToLowerInvariant();
        if (arg is not ("on" or "off"))
            return "Usage: /log-to-file <on|off>";

        var enabled = arg == "on";
        if (settings.BotFileLoggingEnabled == enabled)
            return $"File logging is already {arg}";

        settings.BotFileLoggingEnabled = enabled;
        await settingsService.SaveSettingsAsync(settings);
        return $"File logging turned {arg}";
    }
}
