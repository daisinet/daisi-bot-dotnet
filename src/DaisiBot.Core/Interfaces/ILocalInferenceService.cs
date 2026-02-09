using Daisi.Protos.V1;

namespace DaisiBot.Core.Interfaces;

public interface ILocalInferenceService
{
    bool IsAvailable { get; }
    Task InitializeAsync();
    Task<CreateInferenceResponse> CreateSessionAsync(CreateInferenceRequest request);
    IAsyncEnumerable<SendInferenceResponse> SendAsync(SendInferenceRequest request, CancellationToken cancellationToken = default);
    Task CloseSessionAsync(string inferenceId);
}
