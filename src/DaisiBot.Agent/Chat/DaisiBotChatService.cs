using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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
    private readonly ILocalInferenceService? _localInference;
    private InferenceClient? _inferenceClient;
    private CancellationTokenSource? _streamCts;
    private ChatStats _lastStats = new();

    public event EventHandler<bool>? ConnectionStateChanged;

    public DaisiBotChatService(
        DaisiBotClientKeyProvider keyProvider,
        IConversationStore conversationStore,
        ILoggerFactory loggerFactory,
        ILocalInferenceService? localInference = null)
    {
        _keyProvider = keyProvider;
        _conversationStore = conversationStore;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<DaisiBotChatService>();
        _localInference = localInference;
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

        // Delegate to agent loop if Agent think level
        if (config.ThinkLevel == ConversationThinkLevel.Agent)
        {
            await foreach (var chunk in ExecuteAgentLoopAsync(conversationId, conversation, userMessage, config, _streamCts.Token))
            {
                yield return chunk;
            }
            yield break;
        }

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
            await _inferenceClient.CreateAsync(createRequest);
        }
        catch (Exception ex) when (_inferenceClient is not null)
        {
            _logger.LogWarning(ex, "Session may have expired, reconnecting");
            _inferenceClient = CreateInferenceClient();
            ConnectionStateChanged?.Invoke(this, true);
            try
            {
                await _inferenceClient.CreateAsync(createRequest);
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "Failed to create inference session after retry");
                createError = retryEx.Message;
            }
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

        // Get stats then close inference (keep Orc session alive for reuse)
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

        await SafeCloseAsync(closeOrcSession: false);
        _inferenceClient = null;

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

    private async IAsyncEnumerable<StreamChunk> ExecuteAgentLoopAsync(
        Guid conversationId,
        Conversation conversation,
        string userMessage,
        AgentConfig config,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (_inferenceClient is null)
        {
            _inferenceClient = CreateInferenceClient();
            ConnectionStateChanged?.Invoke(this, true);
        }

        // ── Phase 1: Plan ──
        var plan = await ExecutePlanningPhaseAsync(config, userMessage, ct);

        // Close planning session (best effort)
        await SafeCloseAsync(closeOrcSession: false);

        // Fallback: if plan parsing failed, do a single BasicWithTools call
        if (plan is null)
        {
            await foreach (var chunk in ExecuteFallbackAsync(conversationId, conversation, userMessage, config, ct))
            {
                yield return chunk;
            }
            yield break;
        }

        // Emit the plan
        var planPayload = new ActionPlanPayload
        {
            Goal = plan.Goal,
            Steps = plan.Steps.Select(s => new ActionPlanStepPayload
            {
                StepNumber = s.StepNumber,
                Description = s.Description
            }).ToList()
        };
        yield return new StreamChunk(JsonSerializer.Serialize(planPayload), "ActionPlan", false);

        // ── Phase 2: Execute each step ──
        var execError = await CreateExecutionSessionAsync(config);
        if (execError is not null)
        {
            yield return new StreamChunk($"Error creating execution session: {execError}", "Error", true);
            yield break;
        }

        var stepResults = new List<string>();
        foreach (var step in plan.Steps)
        {
            if (ct.IsCancellationRequested) break;

            step.Status = ActionItemStatus.Running;
            yield return new StreamChunk(
                JsonSerializer.Serialize(new ActionStepStartPayload { StepNumber = step.StepNumber }),
                "ActionStepStart", false);

            // Execute step and collect chunks
            var stepChunks = new List<StreamChunk>();
            var stepResult = await ExecuteStepAsync(step, stepResults, userMessage, config, ct, stepChunks);

            // Forward collected chunks
            foreach (var chunk in stepChunks)
            {
                yield return chunk;
            }

            stepResults.Add(stepResult);

            yield return new StreamChunk(
                JsonSerializer.Serialize(new ActionStepResultPayload
                {
                    StepNumber = step.StepNumber,
                    Status = step.Status,
                    Summary = step.Result ?? step.Error ?? "No output"
                }),
                "ActionStepResult", false);
        }

        // Close execution session
        await SafeCloseAsync(closeOrcSession: false);

        // ── Phase 3: Synthesize ──
        var synthError = await CreateSynthesisSessionAsync(config);
        if (synthError is not null)
        {
            yield return new StreamChunk($"Error creating synthesis session: {synthError}", "Error", true);
            yield break;
        }

        var synthChunks = new List<StreamChunk>();
        var fullContent = await ExecuteSynthesisAsync(userMessage, plan, stepResults, config, ct, synthChunks);

        foreach (var chunk in synthChunks)
        {
            yield return chunk;
        }

        // Persist assistant message
        var cleanedContent = ContentCleaner.Clean(fullContent);
        var assistantMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = ChatRole.Assistant,
            Content = cleanedContent,
            Type = ChatMessageType.Text,
            SortOrder = conversation.Messages.Count + 1
        };

        // Get stats then close inference (keep Orc session alive for reuse)
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

        await SafeCloseAsync(closeOrcSession: false);
        _inferenceClient = null;

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

    private async Task<ActionPlan?> ExecutePlanningPhaseAsync(
        AgentConfig config, string userMessage, CancellationToken ct)
    {
        var planCreateRequest = new CreateInferenceRequest
        {
            ModelName = config.ModelName,
            InitializationPrompt = BuildPlanningSystemPrompt(),
            ThinkLevel = ThinkLevels.Basic
        };

        try
        {
            try
            {
                await _inferenceClient!.CreateAsync(planCreateRequest);
            }
            catch (Exception ex) when (_inferenceClient is not null)
            {
                _logger.LogWarning(ex, "Session may have expired during planning, reconnecting");
                _inferenceClient = CreateInferenceClient();
                ConnectionStateChanged?.Invoke(this, true);
                await _inferenceClient.CreateAsync(planCreateRequest);
            }

            var planSendRequest = SendInferenceRequest.CreateDefault();
            planSendRequest.Text = userMessage;
            planSendRequest.Temperature = 0.3f;
            planSendRequest.TopP = config.TopP;
            planSendRequest.MaxTokens = 1024;
            planSendRequest.ThinkLevel = ThinkLevels.Basic;

            var planContent = new StringBuilder();
            var planStream = _inferenceClient.Send(planSendRequest);
            while (await planStream.ResponseStream.MoveNext(ct))
            {
                var chunk = planStream.ResponseStream.Current;
                if (ct.IsCancellationRequested) break;
                planContent.Append(chunk.Content);
            }

            return PlanParser.Parse(planContent.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planning phase failed, falling back to single call");
            return null;
        }
    }

    private async Task<string?> CreateExecutionSessionAsync(AgentConfig config)
    {
        var execCreateRequest = new CreateInferenceRequest
        {
            ModelName = config.ModelName,
            InitializationPrompt = SkillPromptBuilder.BuildSystemPrompt(
                config.InitializationPrompt, config.EnabledSkills),
            ThinkLevel = ThinkLevels.BasicWithTools
        };
        foreach (var toolGroup in config.EnabledToolGroups)
        {
            execCreateRequest.ToolGroups.Add(EnumMapper.ToProtoToolGroup(toolGroup));
        }

        try
        {
            await _inferenceClient!.CreateAsync(execCreateRequest);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create execution session");
            return ex.Message;
        }
    }

    private async Task<string> ExecuteStepAsync(
        ActionItem step, List<string> stepResults, string userMessage,
        AgentConfig config, CancellationToken ct, List<StreamChunk> outChunks)
    {
        var stepContent = new StringBuilder();
        var stepPrompt = BuildStepPrompt(step, stepResults, userMessage);

        try
        {
            var stepSendRequest = SendInferenceRequest.CreateDefault();
            stepSendRequest.Text = stepPrompt;
            stepSendRequest.Temperature = config.Temperature;
            stepSendRequest.TopP = config.TopP;
            stepSendRequest.MaxTokens = config.MaxTokens;
            stepSendRequest.ThinkLevel = ThinkLevels.BasicWithTools;

            var stepStream = _inferenceClient!.Send(stepSendRequest);
            while (await stepStream.ResponseStream.MoveNext(ct))
            {
                var chunk = stepStream.ResponseStream.Current;
                if (ct.IsCancellationRequested) break;

                // Forward tool-related chunks for visibility
                if (chunk.Type is InferenceResponseTypes.Tooling or InferenceResponseTypes.ToolContent)
                {
                    outChunks.Add(new StreamChunk(chunk.Content, chunk.Type.ToString(), false));
                }

                if (chunk.Type is InferenceResponseTypes.Text or InferenceResponseTypes.ToolContent)
                {
                    stepContent.Append(chunk.Content);
                }
            }

            var result = ContentCleaner.Clean(stepContent.ToString());
            step.Status = ActionItemStatus.Complete;
            step.Result = result;
            return $"Step {step.StepNumber}: {result}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Step {StepNumber} failed", step.StepNumber);
            step.Status = ActionItemStatus.Failed;
            step.Error = ex.Message;
            return $"Step {step.StepNumber} failed: {ex.Message}";
        }
    }

    private async Task<string?> CreateSynthesisSessionAsync(AgentConfig config)
    {
        var synthCreateRequest = new CreateInferenceRequest
        {
            ModelName = config.ModelName,
            InitializationPrompt = "You are a helpful assistant. Synthesize the results below into a clear, concise answer for the user.",
            ThinkLevel = ThinkLevels.Basic
        };

        try
        {
            await _inferenceClient!.CreateAsync(synthCreateRequest);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create synthesis session");
            return ex.Message;
        }
    }

    private async Task<string> ExecuteSynthesisAsync(
        string userMessage, ActionPlan plan, List<string> stepResults,
        AgentConfig config, CancellationToken ct, List<StreamChunk> outChunks)
    {
        var synthPrompt = BuildSynthesisPrompt(userMessage, plan, stepResults);
        var synthSendRequest = SendInferenceRequest.CreateDefault();
        synthSendRequest.Text = synthPrompt;
        synthSendRequest.Temperature = 0.5f;
        synthSendRequest.TopP = config.TopP;
        synthSendRequest.MaxTokens = config.MaxTokens;
        synthSendRequest.ThinkLevel = ThinkLevels.Basic;

        var fullContent = new StringBuilder();
        try
        {
            var synthStream = _inferenceClient!.Send(synthSendRequest);
            while (await synthStream.ResponseStream.MoveNext(ct))
            {
                var chunk = synthStream.ResponseStream.Current;
                if (ct.IsCancellationRequested) break;

                if (chunk.Type == InferenceResponseTypes.Text)
                {
                    fullContent.Append(chunk.Content);
                    outChunks.Add(new StreamChunk(chunk.Content, "Text", false));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Synthesis phase failed");
            fullContent.Append($"[Synthesis error: {ex.Message}]");
            outChunks.Add(new StreamChunk($"[Synthesis error: {ex.Message}]", "Text", false));
        }

        return fullContent.ToString();
    }

    private async IAsyncEnumerable<StreamChunk> ExecuteFallbackAsync(
        Guid conversationId,
        Conversation conversation,
        string userMessage,
        AgentConfig config,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Single BasicWithTools call as fallback
        var createRequest = new CreateInferenceRequest
        {
            ModelName = config.ModelName,
            InitializationPrompt = SkillPromptBuilder.BuildSystemPrompt(
                config.InitializationPrompt, config.EnabledSkills),
            ThinkLevel = ThinkLevels.BasicWithTools
        };
        foreach (var toolGroup in config.EnabledToolGroups)
        {
            createRequest.ToolGroups.Add(EnumMapper.ToProtoToolGroup(toolGroup));
        }

        string? createError = null;
        try
        {
            await _inferenceClient!.CreateAsync(createRequest);
        }
        catch (Exception ex) when (_inferenceClient is not null)
        {
            _logger.LogWarning(ex, "Session may have expired during fallback, reconnecting");
            _inferenceClient = CreateInferenceClient();
            ConnectionStateChanged?.Invoke(this, true);
            try
            {
                await _inferenceClient.CreateAsync(createRequest);
            }
            catch (Exception retryEx)
            {
                createError = retryEx.Message;
            }
        }

        if (createError is not null)
        {
            yield return new StreamChunk($"Error creating fallback session: {createError}", "Error", true);
            yield break;
        }

        var sendRequest = SendInferenceRequest.CreateDefault();
        sendRequest.Text = FormatConversationHistory(conversation.Messages, userMessage);
        sendRequest.Temperature = config.Temperature;
        sendRequest.TopP = config.TopP;
        sendRequest.MaxTokens = config.MaxTokens;
        sendRequest.ThinkLevel = ThinkLevels.BasicWithTools;

        var fullContent = new StringBuilder();
        var lastType = ChatMessageType.Text;

        var stream = _inferenceClient!.Send(sendRequest);
        while (await stream.ResponseStream.MoveNext(ct))
        {
            var chunk = stream.ResponseStream.Current;
            if (ct.IsCancellationRequested) break;

            var chunkType = EnumMapper.FromResponseType(chunk.Type);
            lastType = chunkType;

            if (chunk.Type is InferenceResponseTypes.Text or InferenceResponseTypes.ToolContent)
            {
                fullContent.Append(chunk.Content);
            }

            yield return new StreamChunk(chunk.Content, chunk.Type.ToString(), false);
        }

        var cleanedContent = ContentCleaner.Clean(fullContent.ToString());
        var assistantMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = ChatRole.Assistant,
            Content = cleanedContent,
            Type = lastType,
            SortOrder = conversation.Messages.Count + 1
        };

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

        await SafeCloseAsync(closeOrcSession: false);
        _inferenceClient = null;

        await _conversationStore.AddMessageAsync(assistantMsg);

        if (conversation.Messages.Count <= 1 && conversation.Title == "New Conversation")
        {
            conversation.Title = userMessage.Length > 40
                ? userMessage[..40] + "..."
                : userMessage;
            await _conversationStore.UpdateAsync(conversation);
        }

        yield return new StreamChunk(string.Empty, "Complete", true);
    }

    private async Task SafeCloseAsync(bool closeOrcSession)
    {
        try { await _inferenceClient!.CloseAsync(closeOrcSession: closeOrcSession); }
        catch { /* best effort */ }
    }

    private static string BuildPlanningSystemPrompt() =>
        """
        You are a task planner. Given a user request, break it down into simple steps.

        Rules:
        - Each step must be a single, specific action
        - Use 2 to 5 steps maximum
        - Do NOT include a "summarize" step - that happens automatically
        - Output ONLY the plan in the exact format below

        Format:
        <plan>
        <goal>One sentence describing what the user wants</goal>
        <step>First thing to do</step>
        <step>Second thing to do</step>
        </plan>
        """;

    private static string BuildStepPrompt(ActionItem step, List<string> priorResults, string originalRequest)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Original user request: {originalRequest}");
        sb.AppendLine();

        if (priorResults.Count > 0)
        {
            sb.AppendLine("Results from previous steps:");
            foreach (var result in priorResults)
            {
                sb.AppendLine($"- {result}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"Current task (Step {step.StepNumber}): {step.Description}");
        sb.AppendLine("Complete this step. Be concise and specific.");

        return sb.ToString();
    }

    private static string BuildSynthesisPrompt(string userMessage, ActionPlan plan, List<string> stepResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"The user asked: {userMessage}");
        sb.AppendLine();
        sb.AppendLine($"Goal: {plan.Goal}");
        sb.AppendLine();
        sb.AppendLine("Step results:");
        foreach (var result in stepResults)
        {
            sb.AppendLine($"- {result}");
        }
        sb.AppendLine();
        sb.AppendLine("Synthesize these results into a clear, helpful response for the user. Do not mention the steps or process — just answer naturally.");

        return sb.ToString();
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
