using Daisi.Protos.V1;
using DaisiBot.Agent.Mapping;
using DaisiBot.Core.Enums;

namespace DaisiBot.Agent.Tests.Mapping;

public class EnumMapperTests
{
    // ── ToProto ──

    [Theory]
    [InlineData(ConversationThinkLevel.Basic, ThinkLevels.Basic)]
    [InlineData(ConversationThinkLevel.BasicWithTools, ThinkLevels.BasicWithTools)]
    [InlineData(ConversationThinkLevel.ChainOfThought, ThinkLevels.ChainOfThought)]
    [InlineData(ConversationThinkLevel.TreeOfThought, ThinkLevels.TreeOfThought)]
    public void ToProto_StandardLevels_MapsCorrectly(ConversationThinkLevel input, ThinkLevels expected)
    {
        Assert.Equal(expected, EnumMapper.ToProto(input));
    }

    [Fact]
    public void ToProto_Agent_MapsToBasicWithTools()
    {
        Assert.Equal(ThinkLevels.BasicWithTools, EnumMapper.ToProto(ConversationThinkLevel.Agent));
    }

    [Fact]
    public void ToProto_UnknownValue_DefaultsToBasic()
    {
        Assert.Equal(ThinkLevels.Basic, EnumMapper.ToProto((ConversationThinkLevel)99));
    }

    // ── FromProto ──

    [Theory]
    [InlineData(ThinkLevels.Basic, ConversationThinkLevel.Basic)]
    [InlineData(ThinkLevels.BasicWithTools, ConversationThinkLevel.BasicWithTools)]
    [InlineData(ThinkLevels.ChainOfThought, ConversationThinkLevel.ChainOfThought)]
    [InlineData(ThinkLevels.TreeOfThought, ConversationThinkLevel.TreeOfThought)]
    public void FromProto_StandardLevels_MapsCorrectly(ThinkLevels input, ConversationThinkLevel expected)
    {
        Assert.Equal(expected, EnumMapper.FromProto(input));
    }

    [Fact]
    public void FromProto_UnknownValue_DefaultsToBasic()
    {
        Assert.Equal(ConversationThinkLevel.Basic, EnumMapper.FromProto((ThinkLevels)99));
    }

    // ── ToProtoToolGroup ──

    [Theory]
    [InlineData(ToolGroupSelection.InformationTools, InferenceToolGroups.InformationTools)]
    [InlineData(ToolGroupSelection.FileTools, InferenceToolGroups.FileTools)]
    [InlineData(ToolGroupSelection.MathTools, InferenceToolGroups.MathTools)]
    [InlineData(ToolGroupSelection.CommunicationTools, InferenceToolGroups.CommunicationTools)]
    [InlineData(ToolGroupSelection.CodingTools, InferenceToolGroups.CodingTools)]
    [InlineData(ToolGroupSelection.MediaTools, InferenceToolGroups.MediaTools)]
    [InlineData(ToolGroupSelection.IntegrationTools, InferenceToolGroups.IntegrationTools)]
    [InlineData(ToolGroupSelection.SocialTools, InferenceToolGroups.SocialTools)]
    public void ToProtoToolGroup_AllGroups_MapCorrectly(ToolGroupSelection input, InferenceToolGroups expected)
    {
        Assert.Equal(expected, EnumMapper.ToProtoToolGroup(input));
    }

    [Fact]
    public void ToProtoToolGroup_UnknownValue_DefaultsToInformationTools()
    {
        Assert.Equal(InferenceToolGroups.InformationTools, EnumMapper.ToProtoToolGroup((ToolGroupSelection)99));
    }

    // ── FromResponseType ──

    [Theory]
    [InlineData(InferenceResponseTypes.Text, ChatMessageType.Text)]
    [InlineData(InferenceResponseTypes.Thinking, ChatMessageType.Thinking)]
    [InlineData(InferenceResponseTypes.Tooling, ChatMessageType.Tooling)]
    [InlineData(InferenceResponseTypes.ToolContent, ChatMessageType.ToolContent)]
    [InlineData(InferenceResponseTypes.Error, ChatMessageType.Error)]
    [InlineData(InferenceResponseTypes.Image, ChatMessageType.Image)]
    [InlineData(InferenceResponseTypes.Audio, ChatMessageType.Audio)]
    public void FromResponseType_AllTypes_MapCorrectly(InferenceResponseTypes input, ChatMessageType expected)
    {
        Assert.Equal(expected, EnumMapper.FromResponseType(input));
    }

    [Fact]
    public void FromResponseType_UnknownValue_DefaultsToText()
    {
        Assert.Equal(ChatMessageType.Text, EnumMapper.FromResponseType((InferenceResponseTypes)99));
    }

    // ── Roundtrip ──

    [Theory]
    [InlineData(ConversationThinkLevel.Basic)]
    [InlineData(ConversationThinkLevel.BasicWithTools)]
    [InlineData(ConversationThinkLevel.ChainOfThought)]
    [InlineData(ConversationThinkLevel.TreeOfThought)]
    public void ToProto_FromProto_Roundtrip(ConversationThinkLevel level)
    {
        var proto = EnumMapper.ToProto(level);
        var back = EnumMapper.FromProto(proto);
        Assert.Equal(level, back);
    }

    [Fact]
    public void ToProto_Agent_DoesNotRoundtrip()
    {
        // Agent maps to BasicWithTools, which maps back to BasicWithTools (not Agent)
        var proto = EnumMapper.ToProto(ConversationThinkLevel.Agent);
        var back = EnumMapper.FromProto(proto);
        Assert.Equal(ConversationThinkLevel.BasicWithTools, back);
        Assert.NotEqual(ConversationThinkLevel.Agent, back);
    }
}
