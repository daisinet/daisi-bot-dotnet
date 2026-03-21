using DaisiBot.Agent.Minion;

namespace DaisiBot.Agent.Tests.Minion;

public class MinionMailboxTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MinionMailbox _mailbox;

    public MinionMailboxTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"minion-mailbox-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _mailbox = new MinionMailbox(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void SendAndRead_SingleMessage()
    {
        _mailbox.SendMessage("coder-1", "summoner", "Task complete.");

        var messages = _mailbox.ReadMessages("summoner");

        Assert.Single(messages);
        Assert.Equal("coder-1", messages[0].FromMinionId);
        Assert.Equal("Task complete.", messages[0].Content);
        Assert.True(messages[0].Timestamp > DateTime.MinValue);
    }

    [Fact]
    public void SendAndRead_MultipleMessages_PreservesOrder()
    {
        _mailbox.SendMessage("coder-1", "summoner", "First");
        _mailbox.SendMessage("tester-1", "summoner", "Second");
        _mailbox.SendMessage("coder-1", "summoner", "Third");

        var messages = _mailbox.ReadMessages("summoner");

        Assert.Equal(3, messages.Count);
        Assert.Equal("First", messages[0].Content);
        Assert.Equal("Second", messages[1].Content);
        Assert.Equal("Third", messages[2].Content);
    }

    [Fact]
    public void SendAndRead_DifferentRecipients_Isolated()
    {
        _mailbox.SendMessage("summoner", "coder-1", "Go fix auth.");
        _mailbox.SendMessage("summoner", "tester-1", "Write tests.");

        var coderMessages = _mailbox.ReadMessages("coder-1");
        var testerMessages = _mailbox.ReadMessages("tester-1");

        Assert.Single(coderMessages);
        Assert.Equal("Go fix auth.", coderMessages[0].Content);

        Assert.Single(testerMessages);
        Assert.Equal("Write tests.", testerMessages[0].Content);
    }

    [Fact]
    public void ReadMessages_ClearsInbox_ByDefault()
    {
        _mailbox.SendMessage("coder-1", "summoner", "Done.");

        var first = _mailbox.ReadMessages("summoner");
        Assert.Single(first);

        var second = _mailbox.ReadMessages("summoner");
        Assert.Empty(second);
    }

    [Fact]
    public void ReadMessages_PreservesInbox_WhenClearFalse()
    {
        _mailbox.SendMessage("coder-1", "summoner", "Done.");

        var first = _mailbox.ReadMessages("summoner", clear: false);
        Assert.Single(first);

        var second = _mailbox.ReadMessages("summoner", clear: false);
        Assert.Single(second);
    }

    [Fact]
    public void ReadMessages_EmptyInbox_ReturnsEmpty()
    {
        var messages = _mailbox.ReadMessages("nonexistent-minion");
        Assert.Empty(messages);
    }

    [Fact]
    public void SendMessage_CreatesDirectoryStructure()
    {
        _mailbox.SendMessage("coder-1", "new-minion", "Hello!");

        var inboxDir = Path.Combine(_tempDir, ".minion", "new-minion");
        Assert.True(Directory.Exists(inboxDir));
        Assert.True(File.Exists(Path.Combine(inboxDir, "inbox.json")));
    }

    [Fact]
    public void ProtocolMessage_RoundTrips_ThroughMailbox()
    {
        // Send a structured protocol message as content
        var envelope = MinionProtocol.CreateMessage(
            MinionProtocol.TypeComplete, "coder-1", "Auth module fixed.",
            taskId: "task-3", files: ["src/Auth.cs", "tests/AuthTests.cs"]);

        _mailbox.SendMessage("coder-1", "summoner", envelope);

        var messages = _mailbox.ReadMessages("summoner");
        Assert.Single(messages);

        // Parse the protocol message from the mailbox content
        var parsed = MinionProtocol.ParseMessage(messages[0].Content);
        Assert.NotNull(parsed);
        Assert.Equal(MinionProtocol.TypeComplete, parsed.Type);
        Assert.Equal("coder-1", parsed.From);
        Assert.Equal("Auth module fixed.", parsed.Content);
        Assert.Equal("task-3", parsed.TaskId);
        Assert.Equal(2, parsed.Files!.Length);
    }

    [Fact]
    public void BidirectionalCommunication_SummonerAndMinion()
    {
        // Summoner sends directive to minion
        var directive = MinionProtocol.CreateMessage(
            MinionProtocol.TypeDirective, "summoner", "Focus on the login bug first.");
        _mailbox.SendMessage("summoner", "coder-1", directive);

        // Minion reads and responds
        var inbound = _mailbox.ReadMessages("coder-1");
        Assert.Single(inbound);
        var cmd = MinionProtocol.ParseMessage(inbound[0].Content);
        Assert.Equal(MinionProtocol.TypeDirective, cmd!.Type);

        // Minion sends status back
        var status = MinionProtocol.CreateMessage(
            MinionProtocol.TypeStatus, "coder-1", "Working on login bug.");
        _mailbox.SendMessage("coder-1", "summoner", status);

        var response = _mailbox.ReadMessages("summoner");
        Assert.Single(response);
        var parsed = MinionProtocol.ParseMessage(response[0].Content);
        Assert.Equal(MinionProtocol.TypeStatus, parsed!.Type);
    }

    [Fact]
    public void MinionToMinion_PeerCommunication()
    {
        // coder-1 asks tester-1 a question
        var question = MinionProtocol.CreateMessage(
            MinionProtocol.TypeQuestion, "coder-1",
            "Where is the test for LoginService?", replyTo: "tester-1");
        _mailbox.SendMessage("coder-1", "tester-1", question);

        // tester-1 reads and answers
        var inbound = _mailbox.ReadMessages("tester-1");
        Assert.Single(inbound);

        var answer = MinionProtocol.CreateMessage(
            MinionProtocol.TypeAnswer, "tester-1",
            "It's at tests/LoginServiceTests.cs", replyTo: "coder-1");
        _mailbox.SendMessage("tester-1", "coder-1", answer);

        // coder-1 reads the answer
        var reply = _mailbox.ReadMessages("coder-1");
        Assert.Single(reply);
        var parsed = MinionProtocol.ParseMessage(reply[0].Content);
        Assert.Equal(MinionProtocol.TypeAnswer, parsed!.Type);
        Assert.Contains("LoginServiceTests.cs", parsed.Content);
    }
}
