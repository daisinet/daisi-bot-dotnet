using DaisiBot.Core.Enums;

namespace DaisiBot.Core.Models;

public class BotLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BotId { get; set; }
    public int ExecutionNumber { get; set; }
    public BotLogLevel Level { get; set; } = BotLogLevel.Info;
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
