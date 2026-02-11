using DaisiBot.Core.Enums;

namespace DaisiBot.Agent.Tests.Enums;

public class ConversationThinkLevelTests
{
    [Fact]
    public void Agent_HasValue3()
    {
        Assert.Equal(3, (int)ConversationThinkLevel.Agent);
    }

    [Fact]
    public void AllValues_AreDistinct()
    {
        var values = Enum.GetValues<ConversationThinkLevel>();
        var distinct = values.Select(v => (int)v).Distinct().ToList();

        Assert.Equal(values.Length, distinct.Count);
    }

    [Fact]
    public void AllExpectedValues_Exist()
    {
        Assert.True(Enum.IsDefined(ConversationThinkLevel.Basic));
        Assert.True(Enum.IsDefined(ConversationThinkLevel.BasicWithTools));
        Assert.True(Enum.IsDefined(ConversationThinkLevel.Skilled));
        Assert.True(Enum.IsDefined(ConversationThinkLevel.Agent));
    }

    [Fact]
    public void TotalCount_IsFour()
    {
        Assert.Equal(4, Enum.GetValues<ConversationThinkLevel>().Length);
    }
}

public class ActionItemStatusTests
{
    [Fact]
    public void AllExpectedValues_Exist()
    {
        Assert.True(Enum.IsDefined(ActionItemStatus.Pending));
        Assert.True(Enum.IsDefined(ActionItemStatus.Running));
        Assert.True(Enum.IsDefined(ActionItemStatus.Complete));
        Assert.True(Enum.IsDefined(ActionItemStatus.Failed));
        Assert.True(Enum.IsDefined(ActionItemStatus.Skipped));
    }

    [Fact]
    public void TotalCount_IsFive()
    {
        Assert.Equal(5, Enum.GetValues<ActionItemStatus>().Length);
    }
}
