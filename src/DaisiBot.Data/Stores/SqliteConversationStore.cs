using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DaisiBot.Data.Stores;

public class SqliteConversationStore(IDbContextFactory<DaisiBotDbContext> dbFactory) : IConversationStore
{
    public async Task<List<Conversation>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Conversations
            .Where(c => !c.IsArchived)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();
    }

    public async Task<Conversation?> GetAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Conversations
            .Include(c => c.Messages.OrderBy(m => m.SortOrder))
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Conversation> CreateAsync(Conversation conversation)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        return conversation;
    }

    public async Task UpdateAsync(Conversation conversation)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        conversation.UpdatedAt = DateTime.UtcNow;
        db.Conversations.Update(conversation);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var conversation = await db.Conversations.FindAsync(id);
        if (conversation is not null)
        {
            db.Conversations.Remove(conversation);
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(Guid conversationId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();
    }

    public async Task AddMessageAsync(ChatMessage message)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Messages.Add(message);
        await db.SaveChangesAsync();
    }

    public async Task UpdateMessageAsync(ChatMessage message)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Messages.Update(message);
        await db.SaveChangesAsync();
    }
}
