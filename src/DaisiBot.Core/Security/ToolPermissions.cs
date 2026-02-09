using DaisiBot.Core.Enums;

namespace DaisiBot.Core.Security;

public static class ToolPermissions
{
    private static readonly Dictionary<ToolGroupSelection, (ToolPermissionLevel Level, string Description)> Definitions = new()
    {
        [ToolGroupSelection.InformationTools] = (ToolPermissionLevel.Standard, "Search the web, look up facts, and retrieve public information."),
        [ToolGroupSelection.MathTools] = (ToolPermissionLevel.Standard, "Perform calculations, solve equations, and process numerical data."),
        [ToolGroupSelection.CodingTools] = (ToolPermissionLevel.Standard, "Write, analyze, and debug code snippets."),
        [ToolGroupSelection.MediaTools] = (ToolPermissionLevel.Standard, "Generate, edit, and process images, audio, and video."),
        [ToolGroupSelection.SocialTools] = (ToolPermissionLevel.Standard, "Interact with social platforms and public APIs."),
        [ToolGroupSelection.FileTools] = (ToolPermissionLevel.Elevated, "Read, write, and manage files on your local system. This grants access to your filesystem."),
        [ToolGroupSelection.CommunicationTools] = (ToolPermissionLevel.Elevated, "Send emails, messages, and notifications on your behalf."),
        [ToolGroupSelection.IntegrationTools] = (ToolPermissionLevel.Elevated, "Connect to third-party services and APIs with your credentials."),
    };

    public static ToolPermissionLevel GetPermissionLevel(ToolGroupSelection group)
    {
        return Definitions.TryGetValue(group, out var def) ? def.Level : ToolPermissionLevel.Standard;
    }

    public static string GetDescription(ToolGroupSelection group)
    {
        return Definitions.TryGetValue(group, out var def) ? def.Description : string.Empty;
    }

    public static bool IsElevated(ToolGroupSelection group)
    {
        return GetPermissionLevel(group) == ToolPermissionLevel.Elevated;
    }

    public static IReadOnlyDictionary<ToolGroupSelection, (ToolPermissionLevel Level, string Description)> GetAll() => Definitions;
}
