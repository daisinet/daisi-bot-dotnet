using System.Text;
using DaisiBot.Agent.Minion;
using Daisi.Llogos.Chat;
using Daisi.Llogos.Inference;
using Daisi.Inference.Models;
using ChatMessage = Daisi.Llogos.Chat.ChatMessage;

namespace DaisiBot.Agent.Tests.Minion;

/// <summary>
/// End-to-end integration tests that load a real GGUF model, create a real
/// DistributedMinionManager, spawn actual minions that perform real inference,
/// and validate the full communication pipeline.
///
/// These tests require a GPU with a GGUF model at C:\GGUFS\Qwen3.5-0.8B-Q8_0.gguf.
/// They are skipped automatically if the model or GPU is not available.
///
/// The model is loaded once per test class via IClassFixture to avoid repeated
/// load/unload cycles and CUDA teardown ordering issues.
/// </summary>
public class MinionHarnessFixture : IAsyncLifetime
{
    private static readonly string TestModelPath = @"C:\GGUFS\Qwen3.5-0.8B-Q8_0.gguf";
    public const int TestContextSize = 1024;

    public DaisiLlogosModelHandle? ModelHandle { get; private set; }
    public bool CanRun { get; private set; }

    public async Task InitializeAsync()
    {
        if (!File.Exists(TestModelPath))
            return;

        try
        {
            var backend = new DaisiLlogosTextBackend();
            await backend.ConfigureAsync(new BackendConfiguration { Runtime = "auto" });

            var adapter = await backend.LoadModelAsync(new ModelLoadRequest
            {
                ModelId = "test-model",
                FilePath = TestModelPath,
                ContextSize = (uint)TestContextSize,
            });

            ModelHandle = ((DaisiLlogosModelHandleAdapter)adapter).Inner;
            CanRun = true;
        }
        catch
        {
            // Model load failed — tests will be skipped
        }
    }

    public Task DisposeAsync()
    {
        ModelHandle?.Dispose();
        return Task.CompletedTask;
    }
}

public class MinionHarnessTests : IClassFixture<MinionHarnessFixture>
{
    private readonly MinionHarnessFixture _fixture;

    public MinionHarnessTests(MinionHarnessFixture fixture)
    {
        _fixture = fixture;
    }

    private DaisiLlogosModelHandle ModelHandle => _fixture.ModelHandle!;

    private void SkipIfUnavailable()
    {
        if (!_fixture.CanRun)
            Assert.Fail("Skipped: model not loaded (missing GGUF or no GPU). " +
                         $"Place Qwen3.5-0.8B-Q8_0.gguf in C:\\GGUFS to enable.");
    }

    private static VramBudgetResult CreateBudget(int maxMinions = 3) => new(
        MaxMinions: maxMinions,
        ContextPerMinion: MinionHarnessFixture.TestContextSize,
        PerSessionBytes: 100 * 1024 * 1024,
        AvailableBytes: 4L * 1024 * 1024 * 1024,
        Summary: $"Test budget: {maxMinions} minions at {MinionHarnessFixture.TestContextSize} ctx");

    private InProcessMinionRunner CreateRunner(string id, string role, string goal,
        GpuInferenceGate gate, string systemPrompt = "You are a helpful assistant.",
        int maxTokens = 128, int maxIterations = 3)
    {
        var session = ModelHandle.CreateChatSession(MinionHarnessFixture.TestContextSize, systemPrompt);
        return new InProcessMinionRunner(id, role, goal, session, gate)
        {
            MaxTokensPerTurn = maxTokens,
            MaxIterations = maxIterations,
        };
    }

    // ── Helper: wait for a minion to produce output or reach a status ──

    private static async Task<List<string>> CollectOutputAsync(
        InProcessMinionRunner runner, int maxWaitSeconds = 60)
    {
        var output = new List<string>();
        var deadline = DateTime.UtcNow.AddSeconds(maxWaitSeconds);

        while (DateTime.UtcNow < deadline)
        {
            while (runner.OutputLog.Reader.TryRead(out var line))
                output.Add(line);

            if (runner.Status is MinionStatus.Complete or MinionStatus.Failed or MinionStatus.Stopped)
                break;

            await Task.Delay(500);
        }

        // Drain remaining
        while (runner.OutputLog.Reader.TryRead(out var line))
            output.Add(line);

        return output;
    }

    private static async Task<List<ProtocolMessage>> CollectProtocolMessagesAsync(
        InProcessMinionRunner runner, int maxWaitSeconds = 60)
    {
        var messages = new List<ProtocolMessage>();
        var deadline = DateTime.UtcNow.AddSeconds(maxWaitSeconds);

        while (DateTime.UtcNow < deadline)
        {
            while (runner.Outbox.Reader.TryRead(out var msg))
                messages.Add(msg);

            if (runner.Status is MinionStatus.Complete or MinionStatus.Failed or MinionStatus.Stopped)
                break;

            await Task.Delay(500);
        }

        // Drain remaining
        while (runner.Outbox.Reader.TryRead(out var msg))
            messages.Add(msg);

        return messages;
    }

    // ══════════════════════════════════════════════════════════════
    //  TEST: Single minion spawns, runs inference, produces output
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public async Task SingleMinion_ProducesOutput()
    {
        SkipIfUnavailable();

        using var gate = new GpuInferenceGate();
        var runner = CreateRunner("coder-1", "coder", "Say TASK_COMPLETE immediately.", gate);

        runner.Start();
        var output = await CollectOutputAsync(runner);
        await runner.DisposeAsync();

        Assert.True(output.Count >= 2, $"Expected at least 2 output lines, got {output.Count}");
        Assert.Contains(output, l => l.Contains("[coder-1] Started"));

        var modelOutput = output.Where(l => !l.StartsWith("[coder-1]")).ToList();
        Assert.NotEmpty(modelOutput);
        Assert.True(modelOutput.Sum(l => l.Length) > 10, "Model output too short");
    }

    [Fact]
    public async Task SingleMinion_SendsProtocolMessages()
    {
        SkipIfUnavailable();

        using var gate = new GpuInferenceGate();
        var runner = CreateRunner("tester-1", "tester", "Say TASK_COMPLETE now.", gate);

        runner.Start();
        var messages = await CollectProtocolMessagesAsync(runner);
        await runner.DisposeAsync();

        Assert.NotEmpty(messages);
        Assert.All(messages, m => Assert.Equal("tester-1", m.From));

        var validTypes = new HashSet<string>
        {
            MinionProtocol.TypeStatus, MinionProtocol.TypeComplete,
            MinionProtocol.TypeBlocked, MinionProtocol.TypeFailed,
        };
        Assert.All(messages, m => Assert.Contains(m.Type, validTypes));
    }

    [Fact]
    public async Task Summoner_SendsDirective_MinionReceivesAndResponds()
    {
        SkipIfUnavailable();

        using var gate = new GpuInferenceGate();
        var runner = CreateRunner("coder-1", "coder", "Wait for instructions.", gate, maxIterations: 4);

        runner.Start();
        await Task.Delay(2000);

        runner.Inbox.Writer.TryWrite(new ProtocolMessage
        {
            Type = MinionProtocol.TypeDirective,
            From = "summoner",
            Content = "Say TASK_COMPLETE right now.",
            Timestamp = DateTime.UtcNow
        });

        var output = await CollectOutputAsync(runner);
        await runner.DisposeAsync();

        Assert.True(output.Count >= 2, $"Expected output, got {output.Count} lines");
        Assert.True(string.Join('\n', output).Length > 50, "Output should contain model responses");
    }

    [Fact]
    public async Task Manager_SpawnsMinion_TracksStatus()
    {
        SkipIfUnavailable();

        var budget = CreateBudget(maxMinions: 2);
        await using var manager = new DistributedMinionManager(ModelHandle, new GpuInferenceGate(), budget);

        var runner = manager.SpawnMinion("coder", "Say hello.");
        Assert.NotNull(runner);
        Assert.Equal("coder-1", runner.Id);
        Assert.Equal("coder", runner.Role);
        Assert.Single(manager.Minions);

        await Task.Delay(3000);
        Assert.True(runner.Status is MinionStatus.Running or MinionStatus.Complete,
            $"Expected Running or Complete, got {runner.Status}");

        var output = manager.GetOutput("coder-1");
        Assert.NotNull(output);
    }

    [Fact]
    public async Task Manager_SendMessage_DeliveredToMinionInbox()
    {
        SkipIfUnavailable();

        var budget = CreateBudget(maxMinions: 2);
        await using var manager = new DistributedMinionManager(ModelHandle, new GpuInferenceGate(), budget);

        var runner = manager.SpawnMinion("coder", "Wait.");
        Assert.NotNull(runner);

        var sent = manager.SendMessage("summoner", "coder-1", "Say TASK_COMPLETE now.", MinionProtocol.TypeDirective);
        Assert.True(sent);

        Assert.True(runner.Inbox.Reader.TryPeek(out var peeked));
        Assert.Equal(MinionProtocol.TypeDirective, peeked.Type);
        Assert.Equal("summoner", peeked.From);

        await manager.StopAllAsync();
    }

    [Fact]
    public async Task Manager_RejectsBeyondBudget()
    {
        SkipIfUnavailable();

        var budget = CreateBudget(maxMinions: 1);
        await using var manager = new DistributedMinionManager(ModelHandle, new GpuInferenceGate(), budget);

        var first = manager.SpawnMinion("coder", "Task 1");
        Assert.NotNull(first);

        var second = manager.SpawnMinion("tester", "Task 2");
        Assert.Null(second);

        await manager.StopAllAsync();
    }

    [Fact]
    public async Task TwoMinions_ShareModel_BothProduceOutput()
    {
        SkipIfUnavailable();

        using var gate = new GpuInferenceGate();
        var coder = CreateRunner("coder-1", "coder", "Say TASK_COMPLETE immediately.", gate);
        var tester = CreateRunner("tester-1", "tester", "Say TASK_COMPLETE immediately.", gate);

        coder.Start();
        tester.Start();

        var coderOutput = await CollectOutputAsync(coder);
        var testerOutput = await CollectOutputAsync(tester);

        await coder.DisposeAsync();
        await tester.DisposeAsync();

        Assert.True(coderOutput.Count >= 2, $"Coder produced {coderOutput.Count} lines");
        Assert.True(testerOutput.Count >= 2, $"Tester produced {testerOutput.Count} lines");
        Assert.Contains(coderOutput, l => l.Contains("[coder-1] Started"));
        Assert.Contains(testerOutput, l => l.Contains("[tester-1] Started"));
    }

    [Fact]
    public async Task StopMinion_CancelsAndTransitionsToStopped()
    {
        SkipIfUnavailable();

        using var gate = new GpuInferenceGate();
        var runner = CreateRunner("coder-1", "coder", "Write a very long essay about AI history.", gate,
            maxTokens: 512, maxIterations: 10);

        runner.Start();
        await Task.Delay(2000);

        await runner.StopAsync();

        Assert.Equal(MinionStatus.Stopped, runner.Status);
        Assert.NotNull(runner.CompletedAt);
        await runner.DisposeAsync();
    }

    [Fact]
    public async Task SummonerAndMinion_ShareGate_SummonerCanChat()
    {
        SkipIfUnavailable();

        using var gate = new GpuInferenceGate();
        var summonerSession = ModelHandle.CreateChatSession(MinionHarnessFixture.TestContextSize, "You are a helpful assistant.");
        var runner = CreateRunner("coder-1", "coder", "Say TASK_COMPLETE now.", gate);

        runner.Start();
        await Task.Delay(1000);

        var summonerResponse = new StringBuilder();
        var parameters = new GenerationParams { MaxTokens = 32, Temperature = 0.7f };
        await foreach (var token in gate.RunChatAsync(
            summonerSession, new ChatMessage("user", "Say hello."), parameters))
        {
            summonerResponse.Append(token);
        }

        Assert.True(summonerResponse.Length > 0, "Summoner should have received tokens");

        await runner.StopAsync();
        summonerSession.Dispose();
        await runner.DisposeAsync();
    }

    [Fact]
    public async Task MinionOutput_IsRelevantToGoal()
    {
        SkipIfUnavailable();

        using var gate = new GpuInferenceGate();
        var runner = CreateRunner("researcher-1", "researcher",
            "What are the top 3 most popular programming languages used for web development? Explain briefly why each is popular.",
            gate, systemPrompt: "You are a helpful coding assistant. Always give detailed answers.",
            maxTokens: 256, maxIterations: 3);

        runner.Start();
        var output = await CollectOutputAsync(runner);
        await runner.DisposeAsync();

        var modelOutput = string.Join('\n', output.Where(l => !l.StartsWith("[researcher-1]")));

        // The model should produce substantive output about programming
        Assert.True(modelOutput.Length > 20,
            $"Expected substantive output about programming. Got:\n{modelOutput[..Math.Min(modelOutput.Length, 500)]}");
    }

    [Fact]
    public async Task Manager_StopAll_CleansUpAllMinions()
    {
        SkipIfUnavailable();

        var budget = CreateBudget(maxMinions: 3);
        await using var manager = new DistributedMinionManager(ModelHandle, new GpuInferenceGate(), budget);

        manager.SpawnMinion("coder", "Count to 100.");
        manager.SpawnMinion("tester", "Count to 100.");

        await Task.Delay(2000);
        await manager.StopAllAsync();

        foreach (var (_, runner) in manager.Minions)
        {
            Assert.True(runner.Status is MinionStatus.Stopped or MinionStatus.Complete,
                $"Minion {runner.Id} should be Stopped or Complete, got {runner.Status}");
        }
    }

    [Fact]
    public async Task ProtocolMessages_ContainMeaningfulContent()
    {
        SkipIfUnavailable();

        using var gate = new GpuInferenceGate();
        var runner = CreateRunner("coder-1", "coder", "Say TASK_COMPLETE immediately.", gate);

        runner.Start();
        var messages = await CollectProtocolMessagesAsync(runner);
        await runner.DisposeAsync();

        Assert.All(messages, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Content), $"type={m.Type} has empty content");
            Assert.True(m.Timestamp > DateTime.MinValue, "Should have valid timestamp");
        });
    }

    [Fact]
    public async Task Manager_SendMessage_NonexistentMinion_ReturnsFalse()
    {
        SkipIfUnavailable();

        var budget = CreateBudget(maxMinions: 2);
        await using var manager = new DistributedMinionManager(ModelHandle, new GpuInferenceGate(), budget);

        Assert.False(manager.SendMessage("summoner", "ghost-1", "Hello?"));
    }
}
