using DaisiBot.Agent.Minion;

namespace DaisiBot.Agent.Tests.Minion;

public class MinionProtocolTests
{
    [Fact]
    public void CreateMessage_RoundTrips()
    {
        var json = MinionProtocol.CreateMessage(
            MinionProtocol.TypeStatus, "coder-1", "Working on auth module",
            taskId: "task-1", files: ["src/Auth.cs"]);

        var parsed = MinionProtocol.ParseMessage(json);

        Assert.NotNull(parsed);
        Assert.Equal(MinionProtocol.TypeStatus, parsed.Type);
        Assert.Equal("coder-1", parsed.From);
        Assert.Equal("Working on auth module", parsed.Content);
        Assert.Equal("task-1", parsed.TaskId);
        Assert.NotNull(parsed.Files);
        Assert.Single(parsed.Files);
        Assert.Equal("src/Auth.cs", parsed.Files[0]);
    }

    [Fact]
    public void ParseMessage_InvalidJson_ReturnsTextFallback()
    {
        var parsed = MinionProtocol.ParseMessage("just plain text");

        Assert.NotNull(parsed);
        Assert.Equal("text", parsed.Type);
        Assert.Equal("just plain text", parsed.Content);
    }

    [Fact]
    public void CreateMessage_AllTypes_AreValid()
    {
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
            var json = MinionProtocol.CreateMessage(type, "test", "content");
            var parsed = MinionProtocol.ParseMessage(json);
            Assert.Equal(type, parsed!.Type);
        }
    }

    [Fact]
    public void GetMinionProtocolPrompt_ContainsIdAndRole()
    {
        var prompt = MinionProtocol.GetMinionProtocolPrompt("coder-1", "coder");

        Assert.Contains("coder-1", prompt);
        Assert.Contains("coder", prompt);
        Assert.Contains("send_message", prompt);
    }

    [Fact]
    public void GetSummonerProtocolPrompt_ContainsCoordinationInfo()
    {
        var prompt = MinionProtocol.GetSummonerProtocolPrompt();

        Assert.Contains("summoner", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("directive", prompt);
        Assert.Contains("blocked", prompt);
    }
}
