using System.Runtime.CompilerServices;
using Daisi.Llogos.Chat;
using Daisi.Llogos.Inference;

namespace DaisiBot.Agent.Minion;

/// <summary>
/// Serializes GPU access across the summoner and all in-process minions.
/// Only one ChatAsync forward pass runs at a time. Wraps a SemaphoreSlim(1,1).
/// Both the summoner and minions must acquire the gate before generating tokens.
/// </summary>
public sealed class GpuInferenceGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Run a chat completion through the given session, serialized with all other GPU work.
    /// Acquires the semaphore, streams tokens from session.ChatAsync, then releases.
    /// </summary>
    public async IAsyncEnumerable<string> RunChatAsync(
        DaisiLlogosChatSession session,
        ChatMessage message,
        GenerationParams parameters,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await foreach (var token in session.ChatAsync(message, parameters, ct))
            {
                yield return token;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
