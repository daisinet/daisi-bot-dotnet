using Daisi.Protos.V1;
using DaisiBot.Core.Models;

namespace DaisiBot.Core.Interfaces;

public interface ILocalInferenceService
{
    bool IsAvailable { get; }
    Task InitializeAsync();
    Task<List<ModelDownloadInfo>> GetRequiredDownloadsAsync();
    Task DownloadModelAsync(ModelDownloadInfo model, Action<double, long, long?>? onProgress = null, CancellationToken ct = default);
    Task<CreateInferenceResponse> CreateSessionAsync(CreateInferenceRequest request);
    IAsyncEnumerable<SendInferenceResponse> SendAsync(SendInferenceRequest request, CancellationToken cancellationToken = default);
    Task CloseSessionAsync(string inferenceId);
}
