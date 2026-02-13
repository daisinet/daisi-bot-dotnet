using Daisi.Protos.V1;
using DaisiBot.Core.Enums;

namespace DaisiBot.Agent.Mapping;

public static class EnumMapper
{
    public static ThinkLevels ToProto(ConversationThinkLevel level) => level switch
    {
        ConversationThinkLevel.Basic => ThinkLevels.Basic,
        ConversationThinkLevel.BasicWithTools => ThinkLevels.BasicWithTools,
        ConversationThinkLevel.Skilled => ThinkLevels.Skilled,
        ConversationThinkLevel.Agent => ThinkLevels.Skilled,
        _ => ThinkLevels.Basic
    };

    public static ConversationThinkLevel FromProto(ThinkLevels level) => level switch
    {
        ThinkLevels.Basic => ConversationThinkLevel.Basic,
        ThinkLevels.BasicWithTools => ConversationThinkLevel.BasicWithTools,
        ThinkLevels.Skilled => ConversationThinkLevel.Skilled,
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
        ToolGroupSelection.ShellTools => InferenceToolGroups.ShellTools,
        ToolGroupSelection.ScreenTools => InferenceToolGroups.ScreenTools,
        ToolGroupSelection.InputTools => InferenceToolGroups.InputTools,
        ToolGroupSelection.ClipboardTools => InferenceToolGroups.ClipboardTools,
        ToolGroupSelection.BrowserTools => InferenceToolGroups.BrowserTools,
        ToolGroupSelection.WindowTools => InferenceToolGroups.WindowTools,
        ToolGroupSelection.SystemTools => InferenceToolGroups.SystemTools,
        ToolGroupSelection.GitTools => InferenceToolGroups.GitTools,
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
