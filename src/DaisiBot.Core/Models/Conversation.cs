using DaisiBot.Core.Enums;

namespace DaisiBot.Core.Models;

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "New Conversation";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string ModelName { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public ConversationThinkLevel ThinkLevel { get; set; } = ConversationThinkLevel.Basic;
    public List<ChatMessage> Messages { get; set; } = [];
    public bool IsArchived { get; set; }
}
