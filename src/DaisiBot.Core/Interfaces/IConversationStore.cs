using DaisiBot.Core.Models;

namespace DaisiBot.Core.Interfaces;

public interface IConversationStore
{
    Task<List<Conversation>> GetAllAsync();
    Task<Conversation?> GetAsync(Guid id);
    Task<Conversation> CreateAsync(Conversation conversation);
    Task UpdateAsync(Conversation conversation);
    Task DeleteAsync(Guid id);
    Task<List<ChatMessage>> GetMessagesAsync(Guid conversationId);
    Task AddMessageAsync(ChatMessage message);
    Task UpdateMessageAsync(ChatMessage message);
}
