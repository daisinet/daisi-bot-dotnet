using DaisiBot.Core.Enums;
using DaisiBot.Core.Models;

namespace DaisiBot.Agent.Tests.Models;

public class AgentConfigTests
{
    [Fact]
    public void AgentConfig_Defaults()
    {
        var config = new AgentConfig();

        Assert.Equal(string.Empty, config.ModelName);
        Assert.Equal(string.Empty, config.InitializationPrompt);
        Assert.Equal(ConversationThinkLevel.Basic, config.ThinkLevel);
        Assert.Equal(0.7f, config.Temperature);
        Assert.Equal(0.9f, config.TopP);
        Assert.Equal(32000, config.MaxTokens);
        Assert.Empty(config.EnabledToolGroups);
        Assert.Empty(config.EnabledSkills);
    }

    [Fact]
    public void AgentConfig_AgentThinkLevel()
    {
        var config = new AgentConfig { ThinkLevel = ConversationThinkLevel.Agent };
        Assert.Equal(ConversationThinkLevel.Agent, config.ThinkLevel);
    }

    [Fact]
    public void AgentConfig_WithToolGroups()
    {
        var config = new AgentConfig
        {
            EnabledToolGroups =
            [
                ToolGroupSelection.MathTools,
                ToolGroupSelection.InformationTools
            ]
        };

        Assert.Equal(2, config.EnabledToolGroups.Count);
    }
}
