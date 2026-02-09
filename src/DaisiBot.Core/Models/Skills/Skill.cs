using DaisiBot.Core.Enums;

namespace DaisiBot.Core.Models.Skills;

public class Skill
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string IconUrl { get; set; } = string.Empty;
    public List<ToolGroupSelection> RequiredToolGroups { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public SkillVisibility Visibility { get; set; } = SkillVisibility.Private;
    public SkillStatus Status { get; set; } = SkillStatus.Draft;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int DownloadCount { get; set; }
    public string SystemPromptTemplate { get; set; } = string.Empty;
}
