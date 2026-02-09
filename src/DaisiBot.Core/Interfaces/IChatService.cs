using DaisiBot.Core.Models;

namespace DaisiBot.Core.Interfaces;

public record StreamChunk(string Content, string Type, bool IsComplete);

public interface IChatService
{
    IAsyncEnumerable<StreamChunk> SendMessageAsync(Guid conversationId, string userMessage, AgentConfig config, CancellationToken ct = default);
    Task StopGenerationAsync();
    Task<ChatStats> GetCurrentStatsAsync();
    Task CloseSessionAsync();
    event EventHandler<bool>? ConnectionStateChanged;
}
