using System.Runtime.CompilerServices;
using Daisi.Host.Core.Services;
using Daisi.Protos.V1;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
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

    public async Task<List<ModelDownloadInfo>> GetRequiredDownloadsAsync()
    {
        try
        {
            await _settingsService.LoadAsync();

            var settings = _settingsService.Settings;
            var modelPath = settings.Model.ModelFolderPath;
            if (modelPath.StartsWith("."))
                modelPath = Path.Combine(_settingsService.GetRootFolder(), modelPath);

            Directory.CreateDirectory(modelPath);
            var existingFiles = new HashSet<string>(
                Directory.GetFiles(modelPath).Select(Path.GetFileName)!,
                StringComparer.OrdinalIgnoreCase);

            var requiredModels = _modelService.ModelsClient.GetRequiredModels().Models;

            var missing = new List<ModelDownloadInfo>();
            foreach (var m in requiredModels)
            {
                if (!existingFiles.Contains(m.FileName))
                {
                    missing.Add(new ModelDownloadInfo
                    {
                        Name = m.Name,
                        FileName = m.FileName,
                        Url = m.Url,
                        IsDefault = m.IsDefault,
                        IsMultiModal = m.IsMultiModal
                    });
                }
            }

            return missing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check required model downloads");
            return [];
        }
    }

    public async Task DownloadModelAsync(ModelDownloadInfo model, Action<double, long, long?>? onProgress = null, CancellationToken ct = default)
    {
        var aiModel = new AIModel
        {
            Name = model.Name,
            FileName = model.FileName,
            Url = model.Url,
            IsDefault = model.IsDefault,
            IsMultiModal = model.IsMultiModal,
            Enabled = true
        };

        await _modelService.DownloadRequiredModelAsync(aiModel, onProgress, ct);

        // Register the model in host settings if not already present
        var settings = _settingsService.Settings;
        if (!settings.Model.Models.Any(m => m.Name == model.Name))
        {
            if (model.IsDefault)
            {
                foreach (var existing in settings.Model.Models)
                    existing.IsDefault = false;
            }

            settings.Model.Models.Add(new AIModel
            {
                Name = model.Name,
                FileName = model.FileName,
                Enabled = true,
                IsDefault = model.IsDefault,
                IsMultiModal = model.IsMultiModal,
                Url = model.Url
            });

            await _settingsService.SaveAsync();
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
