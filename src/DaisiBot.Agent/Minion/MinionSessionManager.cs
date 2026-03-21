using System.Collections.Concurrent;
using System.Threading.Channels;
using Daisi.Host.Core.Services;
using Daisi.Protos.V1;
using Microsoft.Extensions.Logging;

namespace DaisiBot.Agent.Minion;

/// <summary>
/// Manages per-minion inference sessions that share a single loaded model.
/// Serializes GPU access through a request queue so only one inference runs at a time.
/// </summary>
public sealed class MinionSessionManager : IDisposable
{
    private readonly InferenceService _inferenceService;
    private readonly ILogger<MinionSessionManager> _logger;
    private readonly ConcurrentDictionary<string, MinionSession> _sessions = new();
    private readonly Channel<InferenceWorkItem> _workQueue = Channel.CreateUnbounded<InferenceWorkItem>();
    private readonly CancellationTokenSource _cts = new();
    private Task? _workerTask;

    public IReadOnlyDictionary<string, MinionSession> Sessions => _sessions;

    public MinionSessionManager(InferenceService inferenceService, ILogger<MinionSessionManager> logger)
    {
        _inferenceService = inferenceService;
        _logger = logger;
    }

    public void Start()
    {
        _workerTask = Task.Run(ProcessWorkQueueAsync);
    }

    public async Task<MinionSession> CreateSessionAsync(string minionId, string role, string goal, string modelName, string systemPrompt)
    {
        var sessionId = $"minion-{minionId}-{Guid.NewGuid():N}";

        var createRequest = new CreateInferenceRequest
        {
            ModelName = modelName,
            InitializationPrompt = systemPrompt,
            ThinkLevel = ThinkLevels.Skilled
        };

        var response = await _inferenceService.CreateNewInferenceSessionAsync(createRequest);

        var session = new MinionSession
        {
            SessionId = sessionId,
            InferenceId = response.InferenceId,
            InternalSessionId = response.SessionId,
            MinionId = minionId,
            Role = role,
            Goal = goal,
            CreatedAt = DateTime.UtcNow
        };

        _sessions[sessionId] = session;
        _logger.LogInformation("Created minion session {SessionId} for minion {MinionId}", sessionId, minionId);
        return session;
    }

    public async IAsyncEnumerable<SendInferenceResponse> ChatAsync(
        string sessionId, string content, float temperature, float topP, int maxTokens,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new InvalidOperationException($"Session {sessionId} not found");

        var sendRequest = SendInferenceRequest.CreateDefault();
        sendRequest.InferenceId = session.InferenceId;
        sendRequest.SessionId = session.InternalSessionId;
        sendRequest.Text = content;
        sendRequest.Temperature = temperature;
        sendRequest.TopP = topP;
        sendRequest.MaxTokens = maxTokens;
        sendRequest.ThinkLevel = ThinkLevels.Skilled;

        // Enqueue work and wait for results via a channel
        var resultChannel = Channel.CreateUnbounded<SendInferenceResponse>();
        var workItem = new InferenceWorkItem(sendRequest, resultChannel.Writer, ct);

        await _workQueue.Writer.WriteAsync(workItem, ct);

        await foreach (var response in resultChannel.Reader.ReadAllAsync(ct))
        {
            session.TokensUsed = response.SessionTokenCount;
            yield return response;
        }
    }

    public async Task AddToolResultAsync(string sessionId, string toolName, string result)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new InvalidOperationException($"Session {sessionId} not found");

        var sendRequest = SendInferenceRequest.CreateDefault();
        sendRequest.InferenceId = session.InferenceId;
        sendRequest.SessionId = session.InternalSessionId;
        sendRequest.Text = $"Tool result from {toolName}: {result}";
        sendRequest.MaxTokens = 1; // Just inject, don't generate

        // Fire and forget through queue
        var resultChannel = Channel.CreateUnbounded<SendInferenceResponse>();
        var workItem = new InferenceWorkItem(sendRequest, resultChannel.Writer, CancellationToken.None);
        await _workQueue.Writer.WriteAsync(workItem);

        // Drain results
        await foreach (var _ in resultChannel.Reader.ReadAllAsync()) { }
    }

    public async IAsyncEnumerable<SendInferenceResponse> ResumeAsync(
        string sessionId, float temperature, float topP, int maxTokens,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new InvalidOperationException($"Session {sessionId} not found");

        var sendRequest = SendInferenceRequest.CreateDefault();
        sendRequest.InferenceId = session.InferenceId;
        sendRequest.SessionId = session.InternalSessionId;
        sendRequest.Text = ""; // Resume with no new input
        sendRequest.Temperature = temperature;
        sendRequest.TopP = topP;
        sendRequest.MaxTokens = maxTokens;
        sendRequest.ThinkLevel = ThinkLevels.Skilled;

        var resultChannel = Channel.CreateUnbounded<SendInferenceResponse>();
        var workItem = new InferenceWorkItem(sendRequest, resultChannel.Writer, ct);
        await _workQueue.Writer.WriteAsync(workItem, ct);

        await foreach (var response in resultChannel.Reader.ReadAllAsync(ct))
        {
            session.TokensUsed = response.SessionTokenCount;
            yield return response;
        }
    }

    public (int tokensUsed, int tokensTotal) GetContextUsage(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new InvalidOperationException($"Session {sessionId} not found");

        return (session.TokensUsed, session.TokensTotal);
    }

    public async Task CloseSessionAsync(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            try
            {
                await _inferenceService.CloseInferenceSessionAsync(
                    session.InferenceId, InferenceCloseReasons.CloseRequestedByClient);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing inference session for minion {MinionId}", session.MinionId);
            }
        }
    }

    private async Task ProcessWorkQueueAsync()
    {
        try
        {
            await foreach (var workItem in _workQueue.Reader.ReadAllAsync(_cts.Token))
            {
                try
                {
                    await foreach (var response in _inferenceService.SendAsync(workItem.Request, workItem.CancellationToken))
                    {
                        await workItem.ResultWriter.WriteAsync(response, workItem.CancellationToken);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing inference work item");
                }
                finally
                {
                    workItem.ResultWriter.Complete();
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        _workQueue.Writer.Complete();

        if (_workerTask is not null)
            await _workerTask;

        // Close all sessions
        foreach (var sessionId in _sessions.Keys.ToList())
        {
            await CloseSessionAsync(sessionId);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _workQueue.Writer.TryComplete();
        _cts.Dispose();
    }

    private sealed record InferenceWorkItem(
        SendInferenceRequest Request,
        ChannelWriter<SendInferenceResponse> ResultWriter,
        CancellationToken CancellationToken);
}

public sealed class MinionSession
{
    public required string SessionId { get; init; }
    public required string InferenceId { get; init; }
    public required string InternalSessionId { get; init; }
    public required string MinionId { get; init; }
    public required string Role { get; init; }
    public required string Goal { get; init; }
    public required DateTime CreatedAt { get; init; }
    public int TokensUsed { get; set; }
    public int TokensTotal { get; set; } = 8192;
    public List<MinionMailMessage> Inbox { get; } = [];
}

public sealed class MinionMailMessage
{
    public required string FromMinionId { get; init; }
    public required string Content { get; init; }
    public required DateTime Timestamp { get; init; }
}
