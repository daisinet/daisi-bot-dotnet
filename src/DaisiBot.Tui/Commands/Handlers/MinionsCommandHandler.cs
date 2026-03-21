using System.Text;
using DaisiBot.Agent.Minion;
using DaisiBot.Core.Models;

namespace DaisiBot.Tui.Commands.Handlers;

/// <summary>
/// Quick /minions command to list spawned minions.
/// Reads from distributed manager or process manager depending on active mode.
/// </summary>
public class MinionsCommandHandler
{
    private readonly SlashCommandDispatcher _dispatcher;

    public MinionsCommandHandler(SlashCommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task<string?> HandleAsync(SlashCommand command)
    {
        var handler = _dispatcher.SummonHandler;
        if (handler is null || !handler.IsSummoned)
            return Task.FromResult<string?>("Not summoned. Use /summon first.");

        // Distributed mode
        if (handler.IsDistributed)
            return Task.FromResult<string?>(FormatDistributedMinions(handler.DistributedManager!));

        // Centralized mode
        return Task.FromResult<string?>(FormatProcessMinions(handler.ProcessManager!));
    }

    private static string FormatDistributedMinions(DistributedMinionManager manager)
    {
        var minions = manager.Minions;
        if (minions.Count == 0)
            return $"No minions spawned yet (distributed mode, max {manager.MaxMinions}). Use /spawn <role> <goal>.";

        var sb = new StringBuilder();
        sb.AppendLine($"Minions ({minions.Count}/{manager.MaxMinions} distributed, {manager.Budget.ContextPerMinion} ctx each):");

        foreach (var (id, runner) in minions)
        {
            var icon = runner.Status switch
            {
                MinionStatus.Running => "●",
                MinionStatus.Complete => "✓",
                MinionStatus.Failed => "✗",
                MinionStatus.Stopped => "■",
                _ => "○"
            };
            var elapsed = (runner.CompletedAt ?? DateTime.UtcNow) - runner.StartedAt;
            sb.AppendLine($"  {icon} {id} [{runner.Role}] {runner.Status} ({elapsed.TotalSeconds:F0}s) - {runner.Goal}");
        }

        return sb.ToString();
    }

    private static string FormatProcessMinions(MinionProcessManager processManager)
    {
        var minions = processManager.Minions;
        if (minions.Count == 0)
            return "No minions spawned yet. Use /spawn <role> <goal>.";

        var sb = new StringBuilder();
        foreach (var (id, info) in minions)
        {
            var icon = info.Status switch
            {
                MinionStatus.Running => "●",
                MinionStatus.Complete => "✓",
                MinionStatus.Failed => "✗",
                MinionStatus.Stopped => "■",
                _ => "○"
            };
            var elapsed = (info.CompletedAt ?? DateTime.UtcNow) - info.StartedAt;
            sb.AppendLine($"  {icon} {id} [{info.Role}] {info.Status} ({elapsed.TotalSeconds:F0}s) - {info.Goal}");
        }

        return sb.ToString();
    }
}
