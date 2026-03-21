using DaisiBot.Core.Models;

namespace DaisiBot.Tui.Commands.Handlers;

/// <summary>
/// Quick /spawn command that delegates to the appropriate minion manager.
/// In distributed mode, creates an in-process minion session.
/// In centralized mode, spawns a headless process via the process manager.
/// Usage: /spawn coder "Fix the auth bug"
/// </summary>
public class SpawnCommandHandler
{
    private readonly SlashCommandDispatcher _dispatcher;

    public SpawnCommandHandler(SlashCommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task<string?> HandleAsync(SlashCommand command)
    {
        var handler = _dispatcher.SummonHandler;
        if (handler is null || !handler.IsSummoned)
            return Task.FromResult<string?>("Not summoned. Use /summon first.");

        if (command.Args.Length < 2)
            return Task.FromResult<string?>("/spawn <role> <goal>\nExample: /spawn coder \"Fix the auth bug\"");

        var role = command.Args[0];
        var goal = string.Join(' ', command.Args[1..]);

        // Distributed mode
        if (handler.IsDistributed)
        {
            var runner = handler.DistributedManager!.SpawnMinion(role, goal);
            if (runner is null)
            {
                var budget = handler.DistributedManager.Budget;
                return Task.FromResult<string?>(
                    $"Cannot spawn: max minions ({budget.MaxMinions}) reached. " +
                    $"Stop a minion first or /unsummon and re-summon with a smaller context.");
            }
            return Task.FromResult<string?>(
                $"Spawned minion '{runner.Id}' as {role} (distributed, in-process): {goal}");
        }

        // Centralized mode
        var info = handler.ProcessManager!.SpawnMinion(role, goal, handler.ServerPort);
        return Task.FromResult<string?>($"Spawned minion '{info.Id}' as {role}: {goal}");
    }
}
