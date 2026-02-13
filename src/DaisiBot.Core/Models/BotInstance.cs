using DaisiBot.Core.Enums;

namespace DaisiBot.Core.Models;

public class BotInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "New Bot";
    public string Goal { get; set; } = string.Empty;
    public string? Persona { get; set; }
    public BotStatus Status { get; set; } = BotStatus.Idle;
    public string? LastError { get; set; }
    public string? RetryGuidance { get; set; }

    // Schedule
    public BotScheduleType ScheduleType { get; set; } = BotScheduleType.Once;
    public int ScheduleIntervalMinutes { get; set; }
    public DateTime? NextRunAt { get; set; }

    // Inference config
    public string ModelName { get; set; } = string.Empty;
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 32000;
    public string EnabledSkillIdsCsv { get; set; } = string.Empty;

    // Interaction
    public string? PendingQuestion { get; set; }
    public int ExecutionCount { get; set; }

    // Memory
    public bool MemoryEnabled { get; set; } = true;
    public int MaxMemoryEntries { get; set; } = 50;

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunAt { get; set; }

    public List<string> GetEnabledSkillIds()
    {
        if (string.IsNullOrWhiteSpace(EnabledSkillIdsCsv)) return [];
        return EnabledSkillIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public void SetEnabledSkillIds(IEnumerable<string> ids)
    {
        EnabledSkillIdsCsv = string.Join(",", ids);
    }
}
