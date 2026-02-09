namespace DaisiBot.Core.Models.Skills;

public class InstalledSkill
{
    public string SkillId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public DateTime EnabledAt { get; set; } = DateTime.UtcNow;
}
