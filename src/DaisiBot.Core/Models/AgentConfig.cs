using DaisiBot.Core.Enums;
using DaisiBot.Core.Models.Skills;

namespace DaisiBot.Core.Models;

public class AgentConfig
{
    public string ModelName { get; set; } = string.Empty;
    public string InitializationPrompt { get; set; } = string.Empty;
    public ConversationThinkLevel ThinkLevel { get; set; } = ConversationThinkLevel.Basic;
    public float Temperature { get; set; } = 0.7f;
    public float TopP { get; set; } = 0.9f;
    public int MaxTokens { get; set; } = 32000;
    public List<ToolGroupSelection> EnabledToolGroups { get; set; } = [];
    public List<Skill> EnabledSkills { get; set; } = [];
}
