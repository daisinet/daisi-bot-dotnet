using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Commands.Handlers;

public class LogInferenceCommandHandler(IServiceProvider services)
{
    public async Task<string?> HandleAsync(SlashCommand command)
    {
        var settingsService = services.GetRequiredService<ISettingsService>();
        var settings = await settingsService.GetSettingsAsync();

        if (command.Args.Length == 0)
            return $"Inference logging is {(settings.LogInferenceOutputEnabled ? "on" : "off")}. Usage: /log-inference <on|off>";

        var arg = command.Args[0].ToLowerInvariant();
        if (arg is not ("on" or "off"))
            return "Usage: /log-inference <on|off>";

        var enabled = arg == "on";
        if (settings.LogInferenceOutputEnabled == enabled)
            return $"Inference logging is already {arg}";

        settings.LogInferenceOutputEnabled = enabled;
        await settingsService.SaveSettingsAsync(settings);
        return $"Inference logging turned {arg}";
    }
}
