using DaisiBot.Core.Enums;
using DaisiBot.Core.Models;

namespace DaisiBot.Agent.Tests.Models;

public class UserSettingsTests
{
    [Fact]
    public void HostModeEnabled_DefaultsFalse()
    {
        var settings = new UserSettings();
        Assert.False(settings.HostModeEnabled);
    }

    [Fact]
    public void NetworkHostEnabled_DefaultsFalse()
    {
        var settings = new UserSettings();
        Assert.False(settings.NetworkHostEnabled);
    }

    [Fact]
    public void ModelFolderPath_DefaultsToEmpty()
    {
        var settings = new UserSettings();
        Assert.Equal(string.Empty, settings.ModelFolderPath);
    }

    [Fact]
    public void ContextSize_DefaultsTo2048()
    {
        var settings = new UserSettings();
        Assert.Equal(2048u, settings.ContextSize);
    }

    [Fact]
    public void GpuLayerCount_DefaultsToNegativeOne()
    {
        var settings = new UserSettings();
        Assert.Equal(-1, settings.GpuLayerCount);
    }

    [Fact]
    public void BatchSize_DefaultsTo512()
    {
        var settings = new UserSettings();
        Assert.Equal(512u, settings.BatchSize);
    }

    [Fact]
    public void DefaultThinkLevel_DefaultsToBasic()
    {
        var settings = new UserSettings();
        Assert.Equal(ConversationThinkLevel.Basic, settings.DefaultThinkLevel);
    }

    [Fact]
    public void DefaultThinkLevel_CanBeSetToAgent()
    {
        var settings = new UserSettings { DefaultThinkLevel = ConversationThinkLevel.Agent };
        Assert.Equal(ConversationThinkLevel.Agent, settings.DefaultThinkLevel);
    }

    [Fact]
    public void GetEnabledToolGroups_EmptyCsv_ReturnsEmptyList()
    {
        var settings = new UserSettings { EnabledToolGroupsCsv = "" };
        Assert.Empty(settings.GetEnabledToolGroups());
    }

    [Fact]
    public void GetEnabledToolGroups_ValidCsv_ParsesCorrectly()
    {
        var settings = new UserSettings { EnabledToolGroupsCsv = "MathTools,FileTools" };
        var groups = settings.GetEnabledToolGroups();

        Assert.Equal(2, groups.Count);
        Assert.Contains(ToolGroupSelection.MathTools, groups);
        Assert.Contains(ToolGroupSelection.FileTools, groups);
    }

    [Fact]
    public void SetEnabledToolGroups_SerializesCorrectly()
    {
        var settings = new UserSettings();
        settings.SetEnabledToolGroups([ToolGroupSelection.CodingTools, ToolGroupSelection.MathTools]);

        Assert.Equal("CodingTools,MathTools", settings.EnabledToolGroupsCsv);
    }

    [Fact]
    public void GetSetToolGroups_Roundtrips()
    {
        var settings = new UserSettings();
        var original = new List<ToolGroupSelection>
        {
            ToolGroupSelection.InformationTools,
            ToolGroupSelection.MediaTools
        };

        settings.SetEnabledToolGroups(original);
        var result = settings.GetEnabledToolGroups();

        Assert.Equal(original, result);
    }

    [Fact]
    public void GetEnabledSkillIds_EmptyCsv_ReturnsEmptyList()
    {
        var settings = new UserSettings { EnabledSkillIdsCsv = "" };
        Assert.Empty(settings.GetEnabledSkillIds());
    }

    [Fact]
    public void GetSetSkillIds_Roundtrips()
    {
        var settings = new UserSettings();
        var ids = new List<string> { "skill-1", "skill-2", "skill-3" };

        settings.SetEnabledSkillIds(ids);
        var result = settings.GetEnabledSkillIds();

        Assert.Equal(ids, result);
    }
}
