using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Commands.Handlers;

public class InstallCommandHandler(IServiceProvider services)
{
    public async Task<string?> HandleAsync(SlashCommand command)
    {
        if (command.Args.Length == 0)
            return "Usage: /install <skill name>";

        var query = command.RawArgs;
        var skillService = services.GetRequiredService<ISkillService>();
        var authService = services.GetRequiredService<IAuthService>();

        var skills = await skillService.GetPublicSkillsAsync(query);
        if (skills.Count == 0)
            return $"No skills found matching '{query}'.";

        var skill = skills[0];
        var auth = await authService.GetAuthStateAsync();
        if (!auth.IsAuthenticated)
            return "You must be logged in to install skills. Use /login first.";

        await skillService.InstallSkillAsync(auth.AccountId, skill.Id);
        return $"Installed skill: {skill.Name} v{skill.Version}";
    }
}
