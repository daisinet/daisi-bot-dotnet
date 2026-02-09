using DaisiBot.Agent.Skills;
using DaisiBot.Core.Models.Skills;

namespace DaisiBot.Agent.Tests.Skills;

public class SkillPromptBuilderTests
{
    [Fact]
    public void BuildSystemPrompt_NoBaseNoSkills_ReturnsEmpty()
    {
        var result = SkillPromptBuilder.BuildSystemPrompt("", []);
        Assert.Equal("", result);
    }

    [Fact]
    public void BuildSystemPrompt_BasePromptOnly_ReturnsBasePrompt()
    {
        var result = SkillPromptBuilder.BuildSystemPrompt("You are a helpful assistant.", []);
        Assert.Equal("You are a helpful assistant.", result);
    }

    [Fact]
    public void BuildSystemPrompt_BasePromptWithWhitespace_ReturnsTrimmed()
    {
        var result = SkillPromptBuilder.BuildSystemPrompt("Be helpful.  ", []);
        Assert.Equal("Be helpful.", result);
    }

    [Fact]
    public void BuildSystemPrompt_WithSkills_IncludesSkillHeaders()
    {
        var skills = new List<Skill>
        {
            new()
            {
                Name = "WeatherBot",
                Version = "1.0.0",
                SystemPromptTemplate = "You can check the weather."
            }
        };

        var result = SkillPromptBuilder.BuildSystemPrompt("Base prompt.", skills);

        Assert.Contains("Base prompt.", result);
        Assert.Contains("[Skill: WeatherBot v1.0.0]", result);
        Assert.Contains("You can check the weather.", result);
    }

    [Fact]
    public void BuildSystemPrompt_WithSkills_IncludesActiveSkillsHeader()
    {
        var skills = new List<Skill>
        {
            new() { Name = "TestSkill", Version = "2.0", SystemPromptTemplate = "Test prompt" }
        };

        var result = SkillPromptBuilder.BuildSystemPrompt("", skills);

        Assert.Contains("--- Active Skills ---", result);
    }

    [Fact]
    public void BuildSystemPrompt_MultipleSkills_IncludesAll()
    {
        var skills = new List<Skill>
        {
            new() { Name = "SkillA", Version = "1.0", SystemPromptTemplate = "Prompt A" },
            new() { Name = "SkillB", Version = "2.0", SystemPromptTemplate = "Prompt B" }
        };

        var result = SkillPromptBuilder.BuildSystemPrompt("Base.", skills);

        Assert.Contains("[Skill: SkillA v1.0]", result);
        Assert.Contains("Prompt A", result);
        Assert.Contains("[Skill: SkillB v2.0]", result);
        Assert.Contains("Prompt B", result);
    }

    [Fact]
    public void BuildSystemPrompt_SkillWithEmptyTemplate_SkipsSkill()
    {
        var skills = new List<Skill>
        {
            new() { Name = "EmptySkill", Version = "1.0", SystemPromptTemplate = "" },
            new() { Name = "RealSkill", Version = "1.0", SystemPromptTemplate = "Real prompt" }
        };

        var result = SkillPromptBuilder.BuildSystemPrompt("Base.", skills);

        Assert.DoesNotContain("EmptySkill", result);
        Assert.Contains("[Skill: RealSkill v1.0]", result);
    }

    [Fact]
    public void BuildSystemPrompt_NullBasePrompt_HandlesGracefully()
    {
        var skills = new List<Skill>
        {
            new() { Name = "Skill", Version = "1.0", SystemPromptTemplate = "Do things" }
        };

        var result = SkillPromptBuilder.BuildSystemPrompt(null!, skills);

        Assert.Contains("[Skill: Skill v1.0]", result);
        Assert.Contains("Do things", result);
    }
}
