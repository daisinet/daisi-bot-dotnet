using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using DaisiBot.Tui.Commands.Handlers;
using DaisiBot.Tui.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Commands;

public class SlashCommandDispatcher
{
    private readonly Dictionary<string, Func<SlashCommand, Task<string?>>> _handlers = new();

    /// <summary>Currently selected bot (set by the panel on selection change).</summary>
    public BotInstance? CurrentBot { get; set; }

    /// <summary>Currently selected conversation (set by the panel on selection change).</summary>
    public Conversation? CurrentConversation { get; set; }

    /// <summary>Callback invoked after a bot is killed+deleted so the screen can refresh.</summary>
    public Action? OnBotDeleted { get; set; }

    public SlashCommandDispatcher(App app, IServiceProvider services, string context)
    {
        _handlers["help"] = new HelpCommandHandler(app).HandleAsync;
        _handlers["new"] = new NewCommandHandler(app, services, context).HandleAsync;
        _handlers["kill"] = new KillCommandHandler(app, services, this).HandleAsync;
        _handlers["list"] = new ListCommandHandler(services).HandleAsync;
        _handlers["install"] = new InstallCommandHandler(services).HandleAsync;
        _handlers["skills"] = new SkillsCommandHandler(app, services).HandleAsync;
        _handlers["model"] = new ModelCommandHandler(app, services).HandleAsync;
        _handlers["settings"] = new SettingsCommandHandler(app, services).HandleAsync;
        _handlers["login"] = new LoginCommandHandler(app, services).HandleAsync;
        _handlers["clear"] = new ClearCommandHandler().HandleAsync;
        _handlers["export"] = new ExportCommandHandler(services, context).HandleAsync;
        _handlers["status"] = new StatusCommandHandler(services, this).HandleAsync;
        _handlers["runnow"] = new RunNowCommandHandler(services, this).HandleAsync;
    }

    public async Task<string?> DispatchAsync(SlashCommand command)
    {
        if (_handlers.TryGetValue(command.Name, out var handler))
        {
            return await handler(command);
        }
        return $"Unknown command: /{command.Name}. Type /help for a list of commands.";
    }

    public IReadOnlyDictionary<string, Func<SlashCommand, Task<string?>>> Handlers => _handlers;
}
