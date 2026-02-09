using System.Runtime.CompilerServices;
using System.Text;
using Daisi.Protos.V1;
using Daisi.SDK.Clients.V1.Host;
using Daisi.SDK.Clients.V1.Orc;
using Daisi.SDK.Clients.V1.SessionManagers;
using DaisiBot.Agent.Auth;
using DaisiBot.Agent.Mapping;
using DaisiBot.Agent.Processing;
using DaisiBot.Agent.Skills;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace DaisiBot.Agent.Chat;

public class DaisiBotChatService : IChatService
{
    private readonly DaisiBotClientKeyProvider _keyProvider;
    private readonly IConversationStore _conversationStore;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DaisiBotChatService> _logger;
    private InferenceClient? _inferenceClient;
    private CancellationTokenSource? _streamCts;
    private ChatStats _lastStats = new();

    public event EventHandler<bool>? ConnectionStateChanged;

    public DaisiBotChatService(
        DaisiBotClientKeyProvider keyProvider,
        IConversationStore conversationStore,
        ILoggerFactory loggerFactory)
    {
        _keyProvider = keyProvider;
        _conversationStore = conversationStore;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<DaisiBotChatService>();
    }

    public async IAsyncEnumerable<StreamChunk> SendMessageAsync(
        Guid conversationId,
        string userMessage,
        AgentConfig config,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var conversation = await _conversationStore.GetAsync(conversationId);
        if (conversation is null)
            throw new InvalidOperationException("Conversation not found.");

        // Save user message
        var userMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = ChatRole.User,
            Content = userMessage,
            Type = ChatMessageType.Text,
            SortOrder = conversation.Messages.Count
        };
        await _conversationStore.AddMessageAsync(userMsg);

        // Create inference client if needed
        if (_inferenceClient is null)
        {
            _inferenceClient = CreateInferenceClient();
            ConnectionStateChanged?.Invoke(this, true);
        }

        // Create inference session
        var createRequest = new CreateInferenceRequest
        {
            ModelName = config.ModelName,
            InitializationPrompt = SkillPromptBuilder.BuildSystemPrompt(
                config.InitializationPrompt, config.EnabledSkills),
            ThinkLevel = EnumMapper.ToProto(config.ThinkLevel)
        };

        foreach (var toolGroup in config.EnabledToolGroups)
        {
            createRequest.ToolGroups.Add(EnumMapper.ToProtoToolGroup(toolGroup));
        }

        string? createError = null;
        try
        {
            var createResponse = await _inferenceClient.CreateAsync(createRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create inference session");
            createError = ex.Message;
        }

        if (createError is not null)
        {
            yield return new StreamChunk($"Error creating session: {createError}", "Error", true);
            yield break;
        }

        // Build conversation history and send
        var sendRequest = SendInferenceRequest.CreateDefault();
        sendRequest.Text = FormatConversationHistory(conversation.Messages, userMessage);
        sendRequest.Temperature = config.Temperature;
        sendRequest.TopP = config.TopP;
        sendRequest.MaxTokens = config.MaxTokens;
        sendRequest.ThinkLevel = EnumMapper.ToProto(config.ThinkLevel);

        var fullContent = new StringBuilder();
        var lastType = ChatMessageType.Text;

        var stream = _inferenceClient.Send(sendRequest);
        while (await stream.ResponseStream.MoveNext(_streamCts.Token))
        {
            var chunk = stream.ResponseStream.Current;
            if (_streamCts.Token.IsCancellationRequested)
                break;

            var chunkType = EnumMapper.FromResponseType(chunk.Type);
            lastType = chunkType;

            if (chunk.Type is InferenceResponseTypes.Text or InferenceResponseTypes.ToolContent)
            {
                fullContent.Append(chunk.Content);
            }

            yield return new StreamChunk(chunk.Content, chunk.Type.ToString(), false);
        }

        // Clean and persist assistant message
        var cleanedContent = ContentCleaner.Clean(fullContent.ToString());
        var assistantMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = ChatRole.Assistant,
            Content = cleanedContent,
            Type = lastType,
            SortOrder = conversation.Messages.Count + 1
        };

        // Get stats
        try
        {
            var stats = _inferenceClient.Stats(new InferenceStatsRequest());
            assistantMsg.TokenCount = stats.LastMessageTokenCount;
            assistantMsg.ComputeTimeMs = stats.LastMessageComputeTimeMs;
            assistantMsg.TokensPerSecond = stats.LastMessageTokensPerSecond;

            _lastStats = new ChatStats
            {
                LastMessageTokenCount = stats.LastMessageTokenCount,
                SessionTokenCount = stats.SessionTokenCount,
                LastMessageComputeTimeMs = stats.LastMessageComputeTimeMs,
                TokensPerSecond = stats.LastMessageTokensPerSecond
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get inference stats");
        }

        await _conversationStore.AddMessageAsync(assistantMsg);

        // Auto-title on first message
        if (conversation.Messages.Count <= 1 && conversation.Title == "New Conversation")
        {
            conversation.Title = userMessage.Length > 40
                ? userMessage[..40] + "..."
                : userMessage;
            await _conversationStore.UpdateAsync(conversation);
        }

        yield return new StreamChunk(string.Empty, "Complete", true);
    }

    public Task StopGenerationAsync()
    {
        _streamCts?.Cancel();
        return Task.CompletedTask;
    }

    public Task<ChatStats> GetCurrentStatsAsync() => Task.FromResult(_lastStats);

    public async Task CloseSessionAsync()
    {
        if (_inferenceClient is not null)
        {
            try
            {
                await _inferenceClient.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing inference session");
            }
            finally
            {
                _inferenceClient = null;
                ConnectionStateChanged?.Invoke(this, false);
            }
        }
    }

    private InferenceClient CreateInferenceClient()
    {
        var sessionClientFactory = new SessionClientFactory(_keyProvider);
        var sessionManager = new InferenceSessionManager(
            sessionClientFactory,
            _keyProvider,
            _loggerFactory.CreateLogger<InferenceSessionManager>());
        var factory = new InferenceClientFactory(sessionManager);
        return factory.Create();
    }

    private static string FormatConversationHistory(List<ChatMessage> messages, string latestUserMessage)
    {
        var sb = new StringBuilder();

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
                continue;

            var prefix = message.Role switch
            {
                ChatRole.User => "User:",
                ChatRole.Assistant => "Assistant:",
                ChatRole.Tool => "Tool:",
                _ => $"{message.Role}:"
            };

            sb.AppendLine($"{prefix} {message.Content}");
        }

        sb.AppendLine($"User: {latestUserMessage}");
        sb.Append("Assistant:");

        return sb.ToString();
    }
}
