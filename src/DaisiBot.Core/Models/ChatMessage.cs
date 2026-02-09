using DaisiBot.Core.Enums;

namespace DaisiBot.Core.Models;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public ChatMessageType Type { get; set; } = ChatMessageType.Text;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int TokenCount { get; set; }
    public double ComputeTimeMs { get; set; }
    public double TokensPerSecond { get; set; }
    public int SortOrder { get; set; }
}
