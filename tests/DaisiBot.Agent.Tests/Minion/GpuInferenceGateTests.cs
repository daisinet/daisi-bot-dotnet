using DaisiBot.Agent.Minion;

namespace DaisiBot.Agent.Tests.Minion;

public class GpuInferenceGateTests
{
    [Fact]
    public void Dispose_IsIdempotent()
    {
        var gate = new GpuInferenceGate();
        gate.Dispose();
        gate.Dispose(); // should not throw
    }

    [Fact]
    public async Task Cancellation_ThrowsOperationCanceled()
    {
        using var gate = new GpuInferenceGate();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Iterating with a pre-cancelled token should throw OperationCanceledException
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in gate.RunChatAsync(null!, null!, null!, cts.Token))
            {
            }
        });

        Assert.IsAssignableFrom<OperationCanceledException>(ex);
    }
}
