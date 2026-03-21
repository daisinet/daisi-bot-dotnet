using DaisiBot.Agent.Minion.Protos;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace DaisiBot.Agent.Minion;

/// <summary>
/// gRPC service that exposes the loaded model to remote minion clients.
/// Delegates to MinionSessionManager for session lifecycle and serialized GPU access.
/// </summary>
public sealed class MinionInferenceGrpcService : Protos.MinionInference.MinionInferenceBase
{
    private readonly MinionSessionManager _sessionManager;
    private readonly ILogger<MinionInferenceGrpcService> _logger;

    public MinionInferenceGrpcService(MinionSessionManager sessionManager, ILogger<MinionInferenceGrpcService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public override async Task<CreateMinionSessionResponse> CreateSession(
        CreateMinionSessionRequest request, ServerCallContext context)
    {
        _logger.LogInformation("CreateSession: minion={MinionId} role={Role}", request.MinionId, request.Role);

        var session = await _sessionManager.CreateSessionAsync(
            request.MinionId, request.Role, request.Goal,
            request.ModelName, request.SystemPrompt);

        return new CreateMinionSessionResponse
        {
            SessionId = session.SessionId,
            ContextSize = session.TokensTotal
        };
    }

    public override async Task Chat(MinionChatRequest request,
        IServerStreamWriter<MinionTokenResponse> responseStream, ServerCallContext context)
    {
        await foreach (var response in _sessionManager.ChatAsync(
            request.SessionId, request.Content,
            request.Temperature, request.TopP, request.MaxTokens,
            context.CancellationToken))
        {
            await responseStream.WriteAsync(new MinionTokenResponse
            {
                SessionId = request.SessionId,
                Content = response.Content ?? "",
                Type = response.Type.ToString(),
                IsComplete = false,
                TokensUsed = response.SessionTokenCount,
                TokensTotal = 8192
            }, context.CancellationToken);
        }

        await responseStream.WriteAsync(new MinionTokenResponse
        {
            SessionId = request.SessionId,
            Content = "",
            Type = "Complete",
            IsComplete = true
        }, context.CancellationToken);
    }

    public override async Task<MinionEmpty> AddToolResult(
        MinionAddToolResultRequest request, ServerCallContext context)
    {
        await _sessionManager.AddToolResultAsync(request.SessionId, request.ToolName, request.Result);
        return new MinionEmpty();
    }

    public override async Task Resume(MinionResumeRequest request,
        IServerStreamWriter<MinionTokenResponse> responseStream, ServerCallContext context)
    {
        await foreach (var response in _sessionManager.ResumeAsync(
            request.SessionId, request.Temperature, request.TopP, request.MaxTokens,
            context.CancellationToken))
        {
            await responseStream.WriteAsync(new MinionTokenResponse
            {
                SessionId = request.SessionId,
                Content = response.Content ?? "",
                Type = response.Type.ToString(),
                IsComplete = false,
                TokensUsed = response.SessionTokenCount,
                TokensTotal = 8192
            }, context.CancellationToken);
        }

        await responseStream.WriteAsync(new MinionTokenResponse
        {
            SessionId = request.SessionId,
            Content = "",
            Type = "Complete",
            IsComplete = true
        }, context.CancellationToken);
    }

    public override Task<MinionContextUsageResponse> GetContextUsage(
        MinionSessionRequest request, ServerCallContext context)
    {
        var (used, total) = _sessionManager.GetContextUsage(request.SessionId);
        return Task.FromResult(new MinionContextUsageResponse
        {
            SessionId = request.SessionId,
            TokensUsed = used,
            TokensTotal = total,
            UsagePercent = total > 0 ? (float)used / total * 100 : 0
        });
    }

    public override async Task<MinionEmpty> CloseSession(
        MinionSessionRequest request, ServerCallContext context)
    {
        await _sessionManager.CloseSessionAsync(request.SessionId);
        return new MinionEmpty();
    }

    public override Task<MinionEmpty> SendMessage(
        MinionSendMessageRequest request, ServerCallContext context)
    {
        // Find the target session by minion ID
        foreach (var session in _sessionManager.Sessions.Values)
        {
            if (session.MinionId == request.ToMinionId)
            {
                session.Inbox.Add(new MinionMailMessage
                {
                    FromMinionId = request.FromMinionId,
                    Content = request.Content,
                    Timestamp = DateTime.UtcNow
                });
                break;
            }
        }
        return Task.FromResult(new MinionEmpty());
    }

    public override Task<MinionMessagesResponse> ReadMessages(
        MinionSessionRequest request, ServerCallContext context)
    {
        var response = new MinionMessagesResponse();

        if (_sessionManager.Sessions.TryGetValue(request.SessionId, out var session))
        {
            foreach (var msg in session.Inbox)
            {
                response.Messages.Add(new Protos.MinionMessage
                {
                    FromMinionId = msg.FromMinionId,
                    Content = msg.Content,
                    Timestamp = msg.Timestamp.ToString("O")
                });
            }
            session.Inbox.Clear();
        }

        return Task.FromResult(response);
    }
}
