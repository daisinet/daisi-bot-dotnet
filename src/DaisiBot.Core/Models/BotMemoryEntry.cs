namespace DaisiBot.Core.Models;

public class BotMemoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BotId { get; set; }
    public int ExecutionNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
