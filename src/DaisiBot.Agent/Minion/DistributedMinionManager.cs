using System.Collections.Concurrent;
using Daisi.Llogos.Chat;

namespace DaisiBot.Agent.Minion;

/// <summary>
/// Manages in-process minion sessions that share a loaded DaisiLlogosModelHandle.
/// Each minion gets its own KV cache + forward pass (separate session) but shares
/// the model weights on GPU. GPU access is serialized through a GpuInferenceGate.
/// </summary>
public sealed class DistributedMinionManager : IAsyncDisposable
{
    private readonly DaisiLlogosModelHandle _modelHandle;
    private readonly GpuInferenceGate _gate;
    private readonly VramBudgetResult _budget;
    private readonly ConcurrentDictionary<string, InProcessMinionRunner> _minions = new();
    private readonly ConcurrentDictionary<string, int> _roleCounts = new();

    public IReadOnlyDictionary<string, InProcessMinionRunner> Minions => _minions;
    public int MaxMinions => _budget.MaxMinions;
    public int ActiveMinions => _minions.Count(m => m.Value.Status == MinionStatus.Running);
    public VramBudgetResult Budget => _budget;

    public DistributedMinionManager(
        DaisiLlogosModelHandle modelHandle,
        GpuInferenceGate gate,
        VramBudgetResult budget)
    {
        _modelHandle = modelHandle;
        _gate = gate;
        _budget = budget;
    }

    /// <summary>
    /// Spawn a new in-process minion with its own chat session.
    /// Returns null if the budget is exceeded.
    /// </summary>
    public InProcessMinionRunner? SpawnMinion(string role, string goal)
    {
        if (_minions.Count >= _budget.MaxMinions)
            return null;

        var count = _roleCounts.AddOrUpdate(role, 1, (_, c) => c + 1);
        var id = $"{role}-{count}";

        // Build system prompt with protocol instructions
        var systemPrompt = MinionProtocol.GetMinionProtocolPrompt(id, role);

        // Create a chat session with the budget's context size
        var session = _modelHandle.CreateChatSession(_budget.ContextPerMinion, systemPrompt);

        var runner = new InProcessMinionRunner(id, role, goal, session, _gate);
        _minions[id] = runner;
        runner.Start();

        return runner;
    }

    /// <summary>Stop a specific minion by ID.</summary>
    public async Task<bool> StopMinionAsync(string id)
    {
        if (!_minions.TryGetValue(id, out var runner))
            return false;

        await runner.StopAsync();
        return true;
    }

    /// <summary>Stop all minions.</summary>
    public async Task StopAllAsync()
    {
        var tasks = _minions.Values.Select(r => r.StopAsync());
        await Task.WhenAll(tasks);
    }

    /// <summary>Read output from a minion's log channel.</summary>
    public string? GetOutput(string id, int maxLines = 50)
    {
        return _minions.TryGetValue(id, out var runner)
            ? runner.ReadOutput(maxLines)
            : null;
    }

    /// <summary>Send a message to a minion's inbox.</summary>
    public bool SendMessage(string fromId, string toId, string content, string type = "directive")
    {
        if (!_minions.TryGetValue(toId, out var runner))
            return false;

        var message = new ProtocolMessage
        {
            Type = type,
            From = fromId,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        return runner.Inbox.Writer.TryWrite(message);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync();
        foreach (var runner in _minions.Values)
        {
            await runner.DisposeAsync();
        }
        _minions.Clear();
        _gate.Dispose();
    }
}
