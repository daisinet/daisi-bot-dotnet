using System.Threading.Channels;
using DaisiBot.Agent.Minion;

namespace DaisiBot.Agent.Tests.Minion;

/// <summary>
/// Tests for in-memory channel-based communication between minions and the summoner.
/// These test the Channel infrastructure used by InProcessMinionRunner and
/// DistributedMinionManager without requiring a loaded model.
/// </summary>
public class InProcessCommunicationTests
{
    [Fact]
    public async Task Inbox_ReceivesDirective()
    {
        var inbox = Channel.CreateUnbounded<ProtocolMessage>();

        var directive = new ProtocolMessage
        {
            Type = MinionProtocol.TypeDirective,
            From = "summoner",
            Content = "Focus on login bug.",
            Timestamp = DateTime.UtcNow
        };

        await inbox.Writer.WriteAsync(directive);

        Assert.True(inbox.Reader.TryRead(out var received));
        Assert.Equal(MinionProtocol.TypeDirective, received.Type);
        Assert.Equal("Focus on login bug.", received.Content);
    }

    [Fact]
    public async Task Outbox_SendsStatusToSummoner()
    {
        var outbox = Channel.CreateUnbounded<ProtocolMessage>();

        var status = new ProtocolMessage
        {
            Type = MinionProtocol.TypeStatus,
            From = "coder-1",
            Content = "Reading auth module.",
            Timestamp = DateTime.UtcNow
        };

        await outbox.Writer.WriteAsync(status);

        Assert.True(outbox.Reader.TryRead(out var received));
        Assert.Equal(MinionProtocol.TypeStatus, received.Type);
        Assert.Equal("coder-1", received.From);
    }

    [Fact]
    public async Task MultipleMinions_IndependentInboxes()
    {
        var inbox1 = Channel.CreateUnbounded<ProtocolMessage>();
        var inbox2 = Channel.CreateUnbounded<ProtocolMessage>();

        await inbox1.Writer.WriteAsync(new ProtocolMessage
        {
            Type = MinionProtocol.TypeDirective, From = "summoner",
            Content = "Fix auth.", Timestamp = DateTime.UtcNow
        });

        await inbox2.Writer.WriteAsync(new ProtocolMessage
        {
            Type = MinionProtocol.TypeDirective, From = "summoner",
            Content = "Write tests.", Timestamp = DateTime.UtcNow
        });

        Assert.True(inbox1.Reader.TryRead(out var msg1));
        Assert.True(inbox2.Reader.TryRead(out var msg2));

        Assert.Equal("Fix auth.", msg1.Content);
        Assert.Equal("Write tests.", msg2.Content);

        // Each inbox is independent — no cross-contamination
        Assert.False(inbox1.Reader.TryRead(out _));
        Assert.False(inbox2.Reader.TryRead(out _));
    }

    [Fact]
    public async Task OutputLog_CollectsRunnerOutput()
    {
        var outputLog = Channel.CreateUnbounded<string>();

        await outputLog.Writer.WriteAsync("[coder-1] Reading src/Auth.cs...");
        await outputLog.Writer.WriteAsync("[coder-1] Found 3 issues.");
        await outputLog.Writer.WriteAsync("[coder-1] Fixing null reference on line 42.");

        var lines = new List<string>();
        while (outputLog.Reader.TryRead(out var line))
            lines.Add(line);

        Assert.Equal(3, lines.Count);
        Assert.Contains("Found 3 issues", lines[1]);
    }

    [Fact]
    public async Task ProtocolMessage_AllTypes_FlowThroughChannel()
    {
        var channel = Channel.CreateUnbounded<ProtocolMessage>();

        var types = new[]
        {
            MinionProtocol.TypeStatus,
            MinionProtocol.TypeBlocked,
            MinionProtocol.TypeComplete,
            MinionProtocol.TypeFailed,
            MinionProtocol.TypeQuestion,
            MinionProtocol.TypeAnswer,
            MinionProtocol.TypeFileClaim,
            MinionProtocol.TypeHandoff,
            MinionProtocol.TypeDirective,
        };

        foreach (var type in types)
        {
            await channel.Writer.WriteAsync(new ProtocolMessage
            {
                Type = type, From = "test", Content = $"msg-{type}",
                Timestamp = DateTime.UtcNow
            });
        }

        var received = new List<ProtocolMessage>();
        while (channel.Reader.TryRead(out var msg))
            received.Add(msg);

        Assert.Equal(types.Length, received.Count);
        for (int i = 0; i < types.Length; i++)
        {
            Assert.Equal(types[i], received[i].Type);
            Assert.Equal($"msg-{types[i]}", received[i].Content);
        }
    }

    [Fact]
    public async Task ConversationFlow_SummonerDirectsMinion()
    {
        // Simulates the summoner sending a directive and minion responding
        var minionInbox = Channel.CreateUnbounded<ProtocolMessage>();
        var minionOutbox = Channel.CreateUnbounded<ProtocolMessage>();

        // 1. Summoner sends directive
        await minionInbox.Writer.WriteAsync(new ProtocolMessage
        {
            Type = MinionProtocol.TypeDirective,
            From = "summoner",
            Content = "Stop current work. Priority shift to payment bug.",
            Timestamp = DateTime.UtcNow
        });

        // 2. Minion reads directive
        var directive = await minionInbox.Reader.ReadAsync();
        Assert.Equal(MinionProtocol.TypeDirective, directive.Type);

        // 3. Minion acknowledges with status
        await minionOutbox.Writer.WriteAsync(new ProtocolMessage
        {
            Type = MinionProtocol.TypeStatus,
            From = "coder-1",
            Content = "Switching to payment bug.",
            Timestamp = DateTime.UtcNow
        });

        // 4. Minion completes the new task
        await minionOutbox.Writer.WriteAsync(new ProtocolMessage
        {
            Type = MinionProtocol.TypeComplete,
            From = "coder-1",
            Content = "Payment bug fixed. Root cause: null check missing in PaymentService.Process().",
            Files = ["src/PaymentService.cs"],
            TaskId = "task-5",
            Timestamp = DateTime.UtcNow
        });

        // 5. Summoner reads both messages
        var msgs = new List<ProtocolMessage>();
        while (minionOutbox.Reader.TryRead(out var m))
            msgs.Add(m);

        Assert.Equal(2, msgs.Count);
        Assert.Equal(MinionProtocol.TypeStatus, msgs[0].Type);
        Assert.Equal(MinionProtocol.TypeComplete, msgs[1].Type);
        Assert.Equal("task-5", msgs[1].TaskId);
        Assert.Contains("src/PaymentService.cs", msgs[1].Files!);
    }

    [Fact]
    public async Task FileClaim_PreventConflicts()
    {
        // Simulates two minions claiming files, summoner detecting conflict
        var coder1Outbox = Channel.CreateUnbounded<ProtocolMessage>();
        var coder2Outbox = Channel.CreateUnbounded<ProtocolMessage>();
        var coder2Inbox = Channel.CreateUnbounded<ProtocolMessage>();

        // coder-1 claims a file
        await coder1Outbox.Writer.WriteAsync(new ProtocolMessage
        {
            Type = MinionProtocol.TypeFileClaim,
            From = "coder-1",
            Content = "Claiming src/Auth.cs",
            Files = ["src/Auth.cs"],
            Timestamp = DateTime.UtcNow
        });

        // coder-2 also claims the same file
        await coder2Outbox.Writer.WriteAsync(new ProtocolMessage
        {
            Type = MinionProtocol.TypeFileClaim,
            From = "coder-2",
            Content = "Claiming src/Auth.cs",
            Files = ["src/Auth.cs"],
            Timestamp = DateTime.UtcNow
        });

        // Summoner reads both claims and detects the conflict
        coder1Outbox.Reader.TryRead(out var claim1);
        coder2Outbox.Reader.TryRead(out var claim2);

        var overlap = claim1!.Files!.Intersect(claim2!.Files!).ToList();
        Assert.Single(overlap);
        Assert.Equal("src/Auth.cs", overlap[0]);

        // Summoner warns coder-2
        await coder2Inbox.Writer.WriteAsync(new ProtocolMessage
        {
            Type = MinionProtocol.TypeDirective,
            From = "summoner",
            Content = "coder-1 is already editing src/Auth.cs. Wait for them to finish.",
            Timestamp = DateTime.UtcNow
        });

        var warning = await coder2Inbox.Reader.ReadAsync();
        Assert.Equal(MinionProtocol.TypeDirective, warning.Type);
        Assert.Contains("coder-1 is already editing", warning.Content);
    }

    [Fact]
    public async Task BlockedMinion_SummonerHelps()
    {
        var minionOutbox = Channel.CreateUnbounded<ProtocolMessage>();
        var minionInbox = Channel.CreateUnbounded<ProtocolMessage>();

        // Minion reports blocked
        await minionOutbox.Writer.WriteAsync(new ProtocolMessage
        {
            Type = MinionProtocol.TypeBlocked,
            From = "tester-1",
            Content = "Can't find the database connection string. Which config file has it?",
            Timestamp = DateTime.UtcNow
        });

        // Summoner reads and answers
        minionOutbox.Reader.TryRead(out var blocked);
        Assert.Equal(MinionProtocol.TypeBlocked, blocked!.Type);

        await minionInbox.Writer.WriteAsync(new ProtocolMessage
        {
            Type = MinionProtocol.TypeAnswer,
            From = "summoner",
            Content = "The connection string is in appsettings.Development.json under ConnectionStrings:Default.",
            ReplyTo = "tester-1",
            Timestamp = DateTime.UtcNow
        });

        var answer = await minionInbox.Reader.ReadAsync();
        Assert.Equal(MinionProtocol.TypeAnswer, answer.Type);
        Assert.Contains("appsettings.Development.json", answer.Content);
    }
}
