using DaisiBot.Core.Enums;

namespace DaisiBot.Core.Models.Skills;

public class SkillReview
{
    public string Id { get; set; } = string.Empty;
    public string SkillId { get; set; } = string.Empty;
    public string ReviewerEmail { get; set; } = string.Empty;
    public SkillStatus Status { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
