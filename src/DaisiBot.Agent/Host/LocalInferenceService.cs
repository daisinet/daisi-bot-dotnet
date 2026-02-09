using System.Runtime.CompilerServices;
using Daisi.Host.Core.Services;
using Daisi.Protos.V1;
using DaisiBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

using HostSettingsService = Daisi.Host.Core.Services.Interfaces.ISettingsService;

namespace DaisiBot.Agent.Host;

public class LocalInferenceService : ILocalInferenceService
{
    private readonly ModelService _modelService;
    private readonly InferenceService _inferenceService;
    private readonly ToolService _toolService;
    private readonly HostSettingsService _settingsService;
    private readonly ILogger<LocalInferenceService> _logger;
    private bool _initialized;

    public bool IsAvailable => _initialized && _modelService.Default is not null;

    public LocalInferenceService(
        ModelService modelService,
        InferenceService inferenceService,
        ToolService toolService,
        HostSettingsService settingsService,
        ILogger<LocalInferenceService> logger)
    {
        _modelService = modelService;
        _inferenceService = inferenceService;
        _toolService = toolService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            await _settingsService.LoadAsync();
            _toolService.LoadTools();
            _modelService.LoadModels();
            _initialized = true;
            _logger.LogInformation("Local inference initialized. Models loaded: {Count}", _modelService.LocalModels.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize local inference");
        }
    }

    public async Task<CreateInferenceResponse> CreateSessionAsync(CreateInferenceRequest request)
    {
        if (!_initialized)
            await InitializeAsync();

        return await _inferenceService.CreateNewInferenceSessionAsync(request);
    }

    public async IAsyncEnumerable<SendInferenceResponse> SendAsync(
        SendInferenceRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var response in _inferenceService.SendAsync(request, cancellationToken))
        {
            yield return response;
        }
    }

    public async Task CloseSessionAsync(string inferenceId)
    {
        await _inferenceService.CloseInferenceSessionAsync(inferenceId, InferenceCloseReasons.CloseRequestedByClient);
    }
}
