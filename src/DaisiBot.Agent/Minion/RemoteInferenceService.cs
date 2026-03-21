using System.Runtime.CompilerServices;
using Daisi.Protos.V1;
using DaisiBot.Agent.Minion.Protos;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DaisiBot.Agent.Minion;

/// <summary>
/// gRPC client that connects to a summoner's inference server.
/// Implements ILocalInferenceService so it can replace local inference transparently.
/// </summary>
public sealed class RemoteInferenceService : ILocalInferenceService, IAsyncDisposable
{
    private readonly string _serverAddress;
    private readonly ILogger<RemoteInferenceService> _logger;
    private GrpcChannel? _channel;
    private Protos.MinionInference.MinionInferenceClient? _client;
    private string? _sessionId;
    private string _minionId;
    private string _role;
    private string _goal;

    public bool IsAvailable => _client is not null;

    public RemoteInferenceService(string serverAddress, string minionId, string role, string goal, ILogger<RemoteInferenceService> logger)
    {
        _serverAddress = serverAddress;
        _minionId = minionId;
        _role = role;
        _goal = goal;
        _logger = logger;
    }

    public Task InitializeAsync()
    {
        _channel = GrpcChannel.ForAddress(_serverAddress, new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 16 * 1024 * 1024
        });
        _client = new Protos.MinionInference.MinionInferenceClient(_channel);
        _logger.LogInformation("Connected to summoner at {Address}", _serverAddress);
        return Task.CompletedTask;
    }

    public Task<List<ModelDownloadInfo>> GetRequiredDownloadsAsync() => Task.FromResult(new List<ModelDownloadInfo>());

    public Task DownloadModelAsync(ModelDownloadInfo model, Action<double, long, long?>? onProgress = null, CancellationToken ct = default)
        => Task.CompletedTask;

    public async Task<CreateInferenceResponse> CreateSessionAsync(CreateInferenceRequest request)
    {
        EnsureClient();

        var response = await _client!.CreateSessionAsync(new CreateMinionSessionRequest
        {
            ModelName = request.ModelName ?? "",
            SystemPrompt = request.InitializationPrompt ?? "",
            Role = _role,
            Goal = _goal,
            MinionId = _minionId
        });

        _sessionId = response.SessionId;

        return new CreateInferenceResponse
        {
            SessionId = response.SessionId,
            InferenceId = response.SessionId // Use session ID as inference ID for remote
        };
    }

    public async IAsyncEnumerable<SendInferenceResponse> SendAsync(
        SendInferenceRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureClient();

        var sessionId = _sessionId ?? request.InferenceId ?? request.SessionId;

        using var call = _client!.Chat(new MinionChatRequest
        {
            SessionId = sessionId,
            Content = request.Text ?? "",
            Temperature = request.Temperature,
            TopP = request.TopP,
            MaxTokens = request.MaxTokens
        }, cancellationToken: cancellationToken);

        while (await call.ResponseStream.MoveNext(cancellationToken))
        {
            var token = call.ResponseStream.Current;
            if (token.IsComplete)
                break;

            yield return new SendInferenceResponse
            {
                SessionId = sessionId,
                InferenceId = sessionId,
                Content = token.Content,
                Type = MapTokenType(token.Type),
                SessionTokenCount = token.TokensUsed,
                MessageTokenCount = token.TokensUsed
            };
        }
    }

    public async Task CloseSessionAsync(string inferenceId)
    {
        if (_client is not null && _sessionId is not null)
        {
            try
            {
                await _client.CloseSessionAsync(new MinionSessionRequest { SessionId = _sessionId });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing remote session");
            }
            _sessionId = null;
        }
    }

    private void EnsureClient()
    {
        if (_client is null)
            throw new InvalidOperationException("Remote inference service not initialized. Call InitializeAsync first.");
    }

    private static InferenceResponseTypes MapTokenType(string type)
    {
        return type switch
        {
            "InferenceResponseTypesText" or "Text" => InferenceResponseTypes.Text,
            "InferenceResponseTypesThinking" or "Thinking" => InferenceResponseTypes.Thinking,
            "InferenceResponseTypesTooling" or "Tooling" => InferenceResponseTypes.Tooling,
            "InferenceResponseTypesToolContent" or "ToolContent" => InferenceResponseTypes.ToolContent,
            "InferenceResponseTypesError" or "Error" => InferenceResponseTypes.Error,
            _ => InferenceResponseTypes.Text
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_sessionId is not null)
        {
            try { await CloseSessionAsync(_sessionId); } catch { }
        }
        _channel?.Dispose();
    }
}
