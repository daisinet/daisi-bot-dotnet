using Daisi.Protos.V1;
using DaisiBot.Core.Enums;

namespace DaisiBot.Agent.Mapping;

public static class EnumMapper
{
    public static ThinkLevels ToProto(ConversationThinkLevel level) => level switch
    {
        ConversationThinkLevel.Basic => ThinkLevels.Basic,
        ConversationThinkLevel.BasicWithTools => ThinkLevels.BasicWithTools,
        ConversationThinkLevel.ChainOfThought => ThinkLevels.ChainOfThought,
        ConversationThinkLevel.TreeOfThought => ThinkLevels.TreeOfThought,
        ConversationThinkLevel.Agent => ThinkLevels.BasicWithTools,
        _ => ThinkLevels.Basic
    };

    public static ConversationThinkLevel FromProto(ThinkLevels level) => level switch
    {
        ThinkLevels.Basic => ConversationThinkLevel.Basic,
        ThinkLevels.BasicWithTools => ConversationThinkLevel.BasicWithTools,
        ThinkLevels.ChainOfThought => ConversationThinkLevel.ChainOfThought,
        ThinkLevels.TreeOfThought => ConversationThinkLevel.TreeOfThought,
        _ => ConversationThinkLevel.Basic
    };

    public static InferenceToolGroups ToProtoToolGroup(ToolGroupSelection group) => group switch
    {
        ToolGroupSelection.InformationTools => InferenceToolGroups.InformationTools,
        ToolGroupSelection.FileTools => InferenceToolGroups.FileTools,
        ToolGroupSelection.MathTools => InferenceToolGroups.MathTools,
        ToolGroupSelection.CommunicationTools => InferenceToolGroups.CommunicationTools,
        ToolGroupSelection.CodingTools => InferenceToolGroups.CodingTools,
        ToolGroupSelection.MediaTools => InferenceToolGroups.MediaTools,
        ToolGroupSelection.IntegrationTools => InferenceToolGroups.IntegrationTools,
        ToolGroupSelection.SocialTools => InferenceToolGroups.SocialTools,
        _ => InferenceToolGroups.InformationTools
    };

    public static ChatMessageType FromResponseType(InferenceResponseTypes type) => type switch
    {
        InferenceResponseTypes.Text => ChatMessageType.Text,
        InferenceResponseTypes.Thinking => ChatMessageType.Thinking,
        InferenceResponseTypes.Tooling => ChatMessageType.Tooling,
        InferenceResponseTypes.ToolContent => ChatMessageType.ToolContent,
        InferenceResponseTypes.Error => ChatMessageType.Error,
        InferenceResponseTypes.Image => ChatMessageType.Image,
        InferenceResponseTypes.Audio => ChatMessageType.Audio,
        _ => ChatMessageType.Text
    };
}
