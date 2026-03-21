using System.Text;
using System.Threading.Channels;
using Daisi.Llogos.Chat;
using Daisi.Llogos.Inference;

namespace DaisiBot.Agent.Minion;

/// <summary>
/// Runs one in-process minion as an async Task. Owns a DaisiLlogosChatSession
/// (its own KV cache sharing model weights) and communicates via in-memory channels.
/// </summary>
public sealed class InProcessMinionRunner : IAsyncDisposable
{
    private readonly DaisiLlogosChatSession _session;
    private readonly GpuInferenceGate _gate;
    private readonly CancellationTokenSource _cts = new();
    private Task? _runTask;

    public string Id { get; }
    public string Role { get; }
    public string Goal { get; }
    public MinionStatus Status { get; private set; } = MinionStatus.Starting;
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; private set; }

    /// <summary>Inbox for receiving directives from the summoner or other minions.</summary>
    public Channel<ProtocolMessage> Inbox { get; } = Channel.CreateUnbounded<ProtocolMessage>();

    /// <summary>Output log channel for the summoner to read what the minion is producing.</summary>
    public Channel<string> OutputLog { get; } = Channel.CreateUnbounded<string>();

    /// <summary>Protocol messages sent by this minion (status, complete, blocked, etc.).</summary>
    public Channel<ProtocolMessage> Outbox { get; } = Channel.CreateUnbounded<ProtocolMessage>();

    /// <summary>Max tokens per generation pass.</summary>
    public int MaxTokensPerTurn { get; init; } = 4096;

    /// <summary>Max agentic loop iterations.</summary>
    public int MaxIterations { get; init; } = 20;

    public InProcessMinionRunner(
        string id, string role, string goal,
        DaisiLlogosChatSession session,
        GpuInferenceGate gate)
    {
        Id = id;
        Role = role;
        Goal = goal;
        _session = session;
        _gate = gate;
    }

    /// <summary>
    /// Start the minion's agentic loop as a background task.
    /// </summary>
    public void Start()
    {
        _runTask = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        Status = MinionStatus.Running;
        await WriteOutput($"[{Id}] Started as {Role}: {Goal}");

        try
        {
            // Send initial goal to the session
            var goalMessage = new ChatMessage("user",
                $"Your goal: {Goal}\n\n" +
                "Work autonomously to complete this goal. " +
                "Report progress via send_message with type 'status'. " +
                "When done, send a 'complete' message. " +
                "If stuck, send a 'blocked' message.");

            var parameters = new GenerationParams
            {
                MaxTokens = MaxTokensPerTurn,
                Temperature = 0.7f,
                TopP = 0.9f,
                RepetitionPenalty = 1.1f
            };

            // Main agentic loop
            int iteration = 0;
            int maxIterations = MaxIterations;

            while (!ct.IsCancellationRequested && iteration < maxIterations)
            {
                iteration++;

                // Generate response
                var response = new StringBuilder();
                await foreach (var token in _gate.RunChatAsync(_session, goalMessage, parameters, ct))
                {
                    response.Append(token);
                }

                var responseText = response.ToString();
                await WriteOutput(responseText);

                // Check for completion signals in the response
                if (ContainsCompletionSignal(responseText))
                {
                    await SendProtocolMessage(MinionProtocol.TypeComplete,
                        $"Goal complete: {Goal}");
                    break;
                }

                // Check for blocked signals
                if (ContainsBlockedSignal(responseText))
                {
                    await SendProtocolMessage(MinionProtocol.TypeBlocked,
                        $"Blocked on: {Goal}");
                }

                // Check inbox for directives
                var directive = await CheckInbox();
                if (directive is not null)
                {
                    goalMessage = new ChatMessage("user",
                        $"[Directive from {directive.From}]: {directive.Content}");
                }
                else
                {
                    // Continue with a follow-up prompt
                    goalMessage = new ChatMessage("user",
                        "Continue working on your goal. If you're done, clearly state TASK_COMPLETE. " +
                        "If you're blocked, clearly state BLOCKED and explain why.");
                }

                // Send periodic status
                if (iteration % 3 == 0)
                {
                    await SendProtocolMessage(MinionProtocol.TypeStatus,
                        $"Iteration {iteration}/{maxIterations} on: {Goal}");
                }
            }

            Status = MinionStatus.Complete;
            CompletedAt = DateTime.UtcNow;
            await WriteOutput($"[{Id}] Completed after {iteration} iterations.");
        }
        catch (OperationCanceledException)
        {
            Status = MinionStatus.Stopped;
            CompletedAt = DateTime.UtcNow;
            await WriteOutput($"[{Id}] Stopped (cancelled).");
        }
        catch (Exception ex)
        {
            Status = MinionStatus.Failed;
            CompletedAt = DateTime.UtcNow;
            await WriteOutput($"[{Id}] Failed: {ex.Message}");
            await SendProtocolMessage(MinionProtocol.TypeFailed, ex.Message);
        }
    }

    private async Task<ProtocolMessage?> CheckInbox()
    {
        if (Inbox.Reader.TryRead(out var message))
            return message;

        // Non-blocking check
        await Task.CompletedTask;
        return null;
    }

    private async Task WriteOutput(string text)
    {
        await OutputLog.Writer.WriteAsync(text);
    }

    private async Task SendProtocolMessage(string type, string content)
    {
        var msg = new ProtocolMessage
        {
            Type = type,
            From = Id,
            Content = content,
            Timestamp = DateTime.UtcNow
        };
        await Outbox.Writer.WriteAsync(msg);
    }

    private static bool ContainsCompletionSignal(string text) =>
        text.Contains("TASK_COMPLETE", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsBlockedSignal(string text) =>
        text.Contains("BLOCKED", StringComparison.OrdinalIgnoreCase);

    /// <summary>Cancel the minion and wait for it to stop.</summary>
    public async Task StopAsync()
    {
        _cts.Cancel();
        if (_runTask is not null)
        {
            try { await _runTask; }
            catch (OperationCanceledException) { }
        }
        Status = MinionStatus.Stopped;
        CompletedAt ??= DateTime.UtcNow;
    }

    /// <summary>Read all available output from the log channel.</summary>
    public string ReadOutput(int maxLines = 50)
    {
        var lines = new List<string>();
        while (OutputLog.Reader.TryRead(out var line) && lines.Count < maxLines)
        {
            lines.Add(line);
        }
        return string.Join('\n', lines);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _session.Dispose();
        _cts.Dispose();
    }
}
