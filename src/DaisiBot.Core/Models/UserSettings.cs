using DaisiBot.Core.Enums;

namespace DaisiBot.Core.Models;

public class UserSettings
{
    public int Id { get; set; } = 1;
    public string DefaultModelName { get; set; } = string.Empty;
    public ConversationThinkLevel DefaultThinkLevel { get; set; } = ConversationThinkLevel.Basic;
    public float Temperature { get; set; } = 0.7f;
    public float TopP { get; set; } = 0.9f;
    public int MaxTokens { get; set; } = 32000;
    public string SystemPrompt { get; set; } = string.Empty;
    public string OrcDomain { get; set; } = "orc.daisinet.com";
    public int OrcPort { get; set; } = 443;
    public bool OrcUseSsl { get; set; } = true;
    public string NetworkName { get; set; } = "devnet";
    public string EnabledToolGroupsCsv { get; set; } = string.Empty;
    public string EnabledSkillIdsCsv { get; set; } = string.Empty;

    // Host mode settings
    public bool HostModeEnabled { get; set; } = true;
    public bool LocalhostModeEnabled { get; set; }
    public string ModelFolderPath { get; set; } = string.Empty;
    public int LlamaRuntime { get; set; }
    public uint ContextSize { get; set; } = 2048;
    public int GpuLayerCount { get; set; } = -1;
    public uint BatchSize { get; set; } = 512;
    public bool NetworkHostEnabled { get; set; }

    // Logging
    public bool BotFileLoggingEnabled { get; set; }
    public bool LogInferenceOutputEnabled { get; set; }

    // UI state
    public string LastScreen { get; set; } = "bots";
    public bool StatusPanelVisible { get; set; } = true;

    public List<ToolGroupSelection> GetEnabledToolGroups()
    {
        if (string.IsNullOrWhiteSpace(EnabledToolGroupsCsv))
            return [];

        return EnabledToolGroupsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Enum.Parse<ToolGroupSelection>(s.Trim()))
            .ToList();
    }

    public void SetEnabledToolGroups(IEnumerable<ToolGroupSelection> groups)
    {
        EnabledToolGroupsCsv = string.Join(",", groups.Select(g => g.ToString()));
    }

    public List<string> GetEnabledSkillIds()
    {
        if (string.IsNullOrWhiteSpace(EnabledSkillIdsCsv))
            return [];

        return EnabledSkillIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public void SetEnabledSkillIds(IEnumerable<string> ids)
    {
        EnabledSkillIdsCsv = string.Join(",", ids);
    }
}
