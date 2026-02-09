using DaisiBot.Core.Enums;

namespace DaisiBot.Core.Models;

public class AvailableModel
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool IsMultiModal { get; set; }
    public bool HasReasoning { get; set; }
    public bool IsDefault { get; set; }
    public List<ConversationThinkLevel> SupportedThinkLevels { get; set; } = [];
}
