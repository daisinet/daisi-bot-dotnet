using Daisi.Protos.V1;
using DaisiBot.Agent.Auth;
using DaisiBot.Agent.Chat;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DaisiBot.Agent.Tests.Chat;

public class DaisiBotChatServiceTests
{
    private readonly Mock<IConversationStore> _storeMock = new();
    private readonly Mock<ILocalInferenceService> _localInferenceMock = new();
    private readonly DaisiBotClientKeyProvider _keyProvider = new();
    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    private DaisiBotChatService CreateService()
    {
        return new DaisiBotChatService(
            _keyProvider,
            _storeMock.Object,
            _loggerFactory,
            _localInferenceMock.Object);
    }

    private static AgentConfig CreateLocalConfig(
        ConversationThinkLevel thinkLevel = ConversationThinkLevel.Skilled,
        string modelName = "test-model",
        string initPrompt = "You are a helpful assistant.")
    {
        return new AgentConfig
        {
            UseHostMode = true,
            ModelName = modelName,
            InitializationPrompt = initPrompt,
            ThinkLevel = thinkLevel,
            Temperature = 0.7f,
            TopP = 0.9f,
            MaxTokens = 4096,
            EnabledToolGroups = [],
            EnabledSkills = []
        };
    }

    private Conversation SetupConversation(Guid? id = null, List<ChatMessage>? messages = null, string title = "New Conversation")
    {
        var convId = id ?? Guid.NewGuid();
        var conversation = new Conversation
        {
            Id = convId,
            Title = title,
            Messages = messages ?? []
        };
        _storeMock.Setup(s => s.GetAsync(convId)).ReturnsAsync(conversation);
        _storeMock.Setup(s => s.AddMessageAsync(It.IsAny<ChatMessage>())).Returns(Task.CompletedTask);
        _storeMock.Setup(s => s.UpdateAsync(It.IsAny<Conversation>())).Returns(Task.CompletedTask);
        return conversation;
    }

    private void SetupCreateSession(string inferenceId = "inf-123", string sessionId = "sess-456")
    {
        _localInferenceMock.Setup(l => l.IsAvailable).Returns(true);
        _localInferenceMock
            .Setup(l => l.CreateSessionAsync(It.IsAny<CreateInferenceRequest>()))
            .ReturnsAsync(new CreateInferenceResponse { InferenceId = inferenceId, SessionId = sessionId });
    }

    private void SetupSendStream(params SendInferenceResponse[] chunks)
    {
        _localInferenceMock
            .Setup(l => l.SendAsync(It.IsAny<SendInferenceRequest>(), It.IsAny<CancellationToken>()))
            .Returns(chunks.ToAsyncEnumerable());
    }

    private void SetupCloseSession()
    {
        _localInferenceMock
            .Setup(l => l.CloseSessionAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    private static SendInferenceResponse TextChunk(string content, int msgTokens = 0, int sessTokens = 0, int computeMs = 0)
    {
        return new SendInferenceResponse
        {
            Content = content,
            Type = InferenceResponseTypes.Text,
            MessageTokenCount = msgTokens,
            SessionTokenCount = sessTokens,
            ComputeTimeMs = computeMs
        };
    }

    private static SendInferenceResponse ErrorChunk(string content)
    {
        return new SendInferenceResponse
        {
            Content = content,
            Type = InferenceResponseTypes.Error
        };
    }

    private async Task<List<StreamChunk>> CollectChunksAsync(IAsyncEnumerable<StreamChunk> stream)
    {
        var result = new List<StreamChunk>();
        await foreach (var chunk in stream)
            result.Add(chunk);
        return result;
    }

    // ────────────────────────────────────────────────────────────────
    // Start session
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_LocalPath_FirstMessage_CreatesSessionAndStreamsResponse()
    {
        var conversation = SetupConversation();
        SetupCreateSession();
        SetupSendStream(TextChunk("Hello ", msgTokens: 5, sessTokens: 10, computeMs: 500), TextChunk("world!"));
        SetupCloseSession();

        var service = CreateService();
        var chunks = await CollectChunksAsync(
            service.SendMessageAsync(conversation.Id, "Hi", CreateLocalConfig()));

        // Should have text chunks plus the final Complete chunk
        Assert.Contains(chunks, c => c.Content == "Hello ");
        Assert.Contains(chunks, c => c.Content == "world!");
        Assert.True(chunks.Last().IsComplete);
    }

    [Fact]
    public async Task SendMessageAsync_LocalPath_FirstMessage_PassesCorrectCreateRequestFields()
    {
        var conversation = SetupConversation();
        SetupCreateSession();
        SetupSendStream(TextChunk("ok"));
        SetupCloseSession();

        CreateInferenceRequest? capturedRequest = null;
        _localInferenceMock
            .Setup(l => l.CreateSessionAsync(It.IsAny<CreateInferenceRequest>()))
            .Callback<CreateInferenceRequest>(r => capturedRequest = r)
            .ReturnsAsync(new CreateInferenceResponse { InferenceId = "inf-1", SessionId = "sess-1" });

        var config = CreateLocalConfig(modelName: "my-model", initPrompt: "Be helpful.");
        var service = CreateService();
        await CollectChunksAsync(service.SendMessageAsync(conversation.Id, "test", config));

        Assert.NotNull(capturedRequest);
        Assert.Equal("my-model", capturedRequest!.ModelName);
        Assert.Contains("Be helpful.", capturedRequest.InitializationPrompt);
        Assert.Equal(ThinkLevels.Skilled, capturedRequest.ThinkLevel);
    }

    // ────────────────────────────────────────────────────────────────
    // InferenceId bug (expose + regression)
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_LocalPath_SendRequest_HasInferenceId()
    {
        var conversation = SetupConversation();
        SetupCreateSession(inferenceId: "inf-ABC");
        SetupSendStream(TextChunk("ok"));
        SetupCloseSession();

        SendInferenceRequest? capturedSend = null;
        _localInferenceMock
            .Setup(l => l.SendAsync(It.IsAny<SendInferenceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendInferenceRequest, CancellationToken>((r, _) => capturedSend = r)
            .Returns(new[] { TextChunk("ok") }.ToAsyncEnumerable());

        var service = CreateService();
        await CollectChunksAsync(service.SendMessageAsync(conversation.Id, "hello", CreateLocalConfig()));

        Assert.NotNull(capturedSend);
        Assert.Equal("inf-ABC", capturedSend!.InferenceId);
    }

    [Fact]
    public async Task SendMessageAsync_LocalPath_SendRequest_HasSessionId()
    {
        var conversation = SetupConversation();
        SetupCreateSession(sessionId: "sess-XYZ");
        SetupSendStream(TextChunk("ok"));
        SetupCloseSession();

        SendInferenceRequest? capturedSend = null;
        _localInferenceMock
            .Setup(l => l.SendAsync(It.IsAny<SendInferenceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendInferenceRequest, CancellationToken>((r, _) => capturedSend = r)
            .Returns(new[] { TextChunk("ok") }.ToAsyncEnumerable());

        var service = CreateService();
        await CollectChunksAsync(service.SendMessageAsync(conversation.Id, "hello", CreateLocalConfig()));

        Assert.NotNull(capturedSend);
        Assert.Equal("sess-XYZ", capturedSend!.SessionId);
    }

    // ────────────────────────────────────────────────────────────────
    // Close session
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_LocalPath_ClosesSessionWithCorrectInferenceId()
    {
        var conversation = SetupConversation();
        SetupCreateSession(inferenceId: "inf-CLOSE");
        SetupSendStream(TextChunk("done"));
        SetupCloseSession();

        var service = CreateService();
        await CollectChunksAsync(service.SendMessageAsync(conversation.Id, "bye", CreateLocalConfig()));

        _localInferenceMock.Verify(l => l.CloseSessionAsync("inf-CLOSE"), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_LocalPath_CreateFails_DoesNotCallSendOrClose()
    {
        var conversation = SetupConversation();
        _localInferenceMock.Setup(l => l.IsAvailable).Returns(true);
        _localInferenceMock
            .Setup(l => l.CreateSessionAsync(It.IsAny<CreateInferenceRequest>()))
            .ThrowsAsync(new InvalidOperationException("model not loaded"));

        var service = CreateService();
        var chunks = await CollectChunksAsync(service.SendMessageAsync(conversation.Id, "hi", CreateLocalConfig()));

        Assert.Contains(chunks, c => c.Type == "Error" && c.Content.Contains("model not loaded"));
        _localInferenceMock.Verify(l => l.SendAsync(It.IsAny<SendInferenceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _localInferenceMock.Verify(l => l.CloseSessionAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendMessageAsync_LocalPath_StreamError_StillClosesSession()
    {
        var conversation = SetupConversation();
        SetupCreateSession(inferenceId: "inf-ERR");
        SetupSendStream(ErrorChunk("stream exploded"));
        SetupCloseSession();

        var service = CreateService();
        var chunks = await CollectChunksAsync(service.SendMessageAsync(conversation.Id, "test", CreateLocalConfig()));

        // Should still attempt to close
        _localInferenceMock.Verify(l => l.CloseSessionAsync("inf-ERR"), Times.Once);
        Assert.Contains(chunks, c => c.Type == "Error");
    }

    // ────────────────────────────────────────────────────────────────
    // Resume (second message, same conversation)
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_LocalPath_Resume_CreatesNewSession()
    {
        var convId = Guid.NewGuid();
        var priorMessages = new List<ChatMessage>
        {
            new() { ConversationId = convId, Role = ChatRole.User, Content = "first msg" },
            new() { ConversationId = convId, Role = ChatRole.Assistant, Content = "first reply" }
        };
        var conversation = SetupConversation(convId, priorMessages, "first msg");

        SetupCreateSession(inferenceId: "inf-RESUME");
        SetupSendStream(TextChunk("resumed"));
        SetupCloseSession();

        var service = CreateService();
        await CollectChunksAsync(service.SendMessageAsync(convId, "second msg", CreateLocalConfig()));

        _localInferenceMock.Verify(l => l.CreateSessionAsync(It.IsAny<CreateInferenceRequest>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_LocalPath_Resume_HistoryIncludesPriorMessages()
    {
        var convId = Guid.NewGuid();
        var priorMessages = new List<ChatMessage>
        {
            new() { ConversationId = convId, Role = ChatRole.User, Content = "what is 2+2" },
            new() { ConversationId = convId, Role = ChatRole.Assistant, Content = "4" }
        };
        var conversation = SetupConversation(convId, priorMessages, "what is 2+2");

        SetupCreateSession();
        SetupCloseSession();

        SendInferenceRequest? capturedSend = null;
        _localInferenceMock
            .Setup(l => l.SendAsync(It.IsAny<SendInferenceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendInferenceRequest, CancellationToken>((r, _) => capturedSend = r)
            .Returns(new[] { TextChunk("six") }.ToAsyncEnumerable());

        var service = CreateService();
        await CollectChunksAsync(service.SendMessageAsync(convId, "what is 3+3", CreateLocalConfig()));

        Assert.NotNull(capturedSend);
        Assert.Contains("User: what is 2+2", capturedSend!.Text);
        Assert.Contains("Assistant: 4", capturedSend.Text);
        Assert.Contains("User: what is 3+3", capturedSend.Text);
    }

    [Fact]
    public async Task SendMessageAsync_LocalPath_Resume_SystemMessagesExcludedFromHistory()
    {
        var convId = Guid.NewGuid();
        var priorMessages = new List<ChatMessage>
        {
            new() { ConversationId = convId, Role = ChatRole.System, Content = "SECRET SYSTEM PROMPT" },
            new() { ConversationId = convId, Role = ChatRole.User, Content = "hello" },
            new() { ConversationId = convId, Role = ChatRole.Assistant, Content = "hi there" }
        };
        var conversation = SetupConversation(convId, priorMessages, "hello");

        SetupCreateSession();
        SetupCloseSession();

        SendInferenceRequest? capturedSend = null;
        _localInferenceMock
            .Setup(l => l.SendAsync(It.IsAny<SendInferenceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendInferenceRequest, CancellationToken>((r, _) => capturedSend = r)
            .Returns(new[] { TextChunk("ok") }.ToAsyncEnumerable());

        var service = CreateService();
        await CollectChunksAsync(service.SendMessageAsync(convId, "again", CreateLocalConfig()));

        Assert.NotNull(capturedSend);
        Assert.DoesNotContain("SECRET SYSTEM PROMPT", capturedSend!.Text);
        Assert.Contains("User: hello", capturedSend.Text);
    }

    // ────────────────────────────────────────────────────────────────
    // History formatting (direct tests)
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void FormatConversationHistory_EmptyMessages_ReturnsOnlyNewMessage()
    {
        var result = DaisiBotChatService.FormatConversationHistory([], "hello world");

        Assert.Contains("User: hello world", result);
        Assert.EndsWith("Assistant:", result);
    }

    [Fact]
    public void FormatConversationHistory_WithPriorMessages_FormatsAll()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = ChatRole.User, Content = "first" },
            new() { Role = ChatRole.Assistant, Content = "reply" }
        };

        var result = DaisiBotChatService.FormatConversationHistory(messages, "second");

        Assert.Contains("User: first", result);
        Assert.Contains("Assistant: reply", result);
        Assert.Contains("User: second", result);
        Assert.EndsWith("Assistant:", result);
    }

    [Fact]
    public void FormatConversationHistory_SkipsSystemMessages()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = ChatRole.System, Content = "system prompt" },
            new() { Role = ChatRole.User, Content = "hello" }
        };

        var result = DaisiBotChatService.FormatConversationHistory(messages, "world");

        Assert.DoesNotContain("system prompt", result);
        Assert.Contains("User: hello", result);
        Assert.Contains("User: world", result);
    }

    // ────────────────────────────────────────────────────────────────
    // Edge cases
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_ConversationNotFound_ThrowsInvalidOperation()
    {
        _storeMock.Setup(s => s.GetAsync(It.IsAny<Guid>())).ReturnsAsync((Conversation?)null);

        var service = CreateService();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await CollectChunksAsync(service.SendMessageAsync(Guid.NewGuid(), "test", CreateLocalConfig()));
        });
    }

    [Fact]
    public async Task SendMessageAsync_LocalPath_AgentThinkLevel_DowngradesToSkilled()
    {
        var conversation = SetupConversation();
        SetupCreateSession();
        SetupSendStream(TextChunk("ok"));
        SetupCloseSession();

        CreateInferenceRequest? capturedRequest = null;
        _localInferenceMock
            .Setup(l => l.CreateSessionAsync(It.IsAny<CreateInferenceRequest>()))
            .Callback<CreateInferenceRequest>(r => capturedRequest = r)
            .ReturnsAsync(new CreateInferenceResponse { InferenceId = "inf-1", SessionId = "sess-1" });

        var config = CreateLocalConfig(thinkLevel: ConversationThinkLevel.Agent);
        var service = CreateService();
        await CollectChunksAsync(service.SendMessageAsync(conversation.Id, "test", config));

        Assert.NotNull(capturedRequest);
        Assert.Equal(ThinkLevels.Skilled, capturedRequest!.ThinkLevel);
    }

    [Fact]
    public async Task SendMessageAsync_LocalPath_AutoTitle_SetsOnFirstMessage()
    {
        var conversation = SetupConversation(title: "New Conversation");
        SetupCreateSession();
        SetupSendStream(TextChunk("response"));
        SetupCloseSession();

        var service = CreateService();
        await CollectChunksAsync(service.SendMessageAsync(conversation.Id, "My important question", CreateLocalConfig()));

        _storeMock.Verify(s => s.UpdateAsync(It.Is<Conversation>(c => c.Title == "My important question")), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_LocalPath_AutoTitle_SkipsOnResume()
    {
        var convId = Guid.NewGuid();
        var priorMessages = new List<ChatMessage>
        {
            new() { ConversationId = convId, Role = ChatRole.User, Content = "first" },
            new() { ConversationId = convId, Role = ChatRole.Assistant, Content = "reply" }
        };
        var conversation = SetupConversation(convId, priorMessages, "first");

        SetupCreateSession();
        SetupSendStream(TextChunk("second reply"));
        SetupCloseSession();

        var service = CreateService();
        await CollectChunksAsync(service.SendMessageAsync(convId, "second question", CreateLocalConfig()));

        // Title should NOT be updated since this isn't the first message
        _storeMock.Verify(s => s.UpdateAsync(It.Is<Conversation>(c => c.Title == "second question")), Times.Never);
    }

    [Fact]
    public async Task SendMessageAsync_LocalPath_TokenStats_PersistedOnAssistantMessage()
    {
        var conversation = SetupConversation();
        SetupCreateSession();
        SetupSendStream(TextChunk("result", msgTokens: 42, sessTokens: 100, computeMs: 2000));
        SetupCloseSession();

        ChatMessage? savedAssistantMsg = null;
        _storeMock
            .Setup(s => s.AddMessageAsync(It.Is<ChatMessage>(m => m.Role == ChatRole.Assistant)))
            .Callback<ChatMessage>(m => savedAssistantMsg = m)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await CollectChunksAsync(service.SendMessageAsync(conversation.Id, "test", CreateLocalConfig()));

        Assert.NotNull(savedAssistantMsg);
        Assert.Equal(42, savedAssistantMsg!.TokenCount);
        Assert.Equal(2000, savedAssistantMsg.ComputeTimeMs);
        Assert.True(savedAssistantMsg.TokensPerSecond > 0);
    }
}
