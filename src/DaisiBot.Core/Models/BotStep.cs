namespace DaisiBot.Core.Models;

public class BotStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BotId { get; set; }
    public int StepNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
