using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Daisi.Host.Core.Models;
using Daisi.Host.Core.Services;
using Daisi.Protos.V1;
using Daisi.SDK.Clients.V1.Orc;
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
    private readonly ISettingsService _userSettingsService;
    private readonly HostClientFactory _hostClientFactory;
    private readonly IAuthService _authService;
    private readonly ILogger<LocalInferenceService> _logger;
    private bool _initialized;

    public bool IsAvailable => _initialized && _modelService.Default is not null;

    public LocalInferenceService(
        ModelService modelService,
        InferenceService inferenceService,
        ToolService toolService,
        HostSettingsService settingsService,
        ISettingsService userSettingsService,
        HostClientFactory hostClientFactory,
        IAuthService authService,
        ILogger<LocalInferenceService> logger)
    {
        _modelService = modelService;
        _inferenceService = inferenceService;
        _toolService = toolService;
        _settingsService = settingsService;
        _userSettingsService = userSettingsService;
        _hostClientFactory = hostClientFactory;
        _authService = authService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            DiagLog("LocalInferenceService.InitializeAsync starting...");
            await _settingsService.LoadAsync();
            await SyncUserModelPathAsync();
            await EnsureHostRegisteredAsync();
            _toolService.LoadTools();
            _toolService.LoadToolsFromAssembly(typeof(DaisiBot.LocalTools.Shell.ShellExecuteTool).Assembly);
            DiagLog("Tools loaded, loading models...");
            _modelService.LoadModels();
            _initialized = true;
            var ctxSize = _settingsService.Settings?.Model?.Backend?.ContextSize ?? 0;
            DiagLog($"Local inference initialized. Models: {_modelService.LocalModels.Count}, Default: {_modelService.Default?.AIModel.Name ?? "none"}, ContextSize: {ctxSize}");
            _logger.LogInformation("Local inference initialized. Models loaded: {Count}", _modelService.LocalModels.Count);
        }
        catch (Exception ex)
        {
            DiagLog($"InitializeAsync FAILED: {UnwrapException(ex)}");
            _logger.LogError(ex, "Failed to initialize local inference");
        }
    }

    private async Task EnsureHostRegisteredAsync()
    {
        try
        {
            var authState = await _authService.GetAuthStateAsync();
            if (!authState.IsAuthenticated)
                return;

            if (!string.IsNullOrWhiteSpace(_settingsService.Settings.Host?.SecretKey))
                return;

            var hostClient = _hostClientFactory.Create();
            var response = await hostClient.RegisterAsync(new RegisterHostRequest
            {
                Host = new Daisi.Protos.V1.Host
                {
                    Name = Environment.MachineName,
                    Port = 0,
                    Region = "USSouthEast",
                    OperatingSystem = RuntimeInformation.OSDescription,
                    OperatingSystemVersion = Environment.OSVersion.VersionString
                }
            });

            _settingsService.Settings.Host.SecretKey = response.SecretKey;
            _settingsService.Settings.Host.Id = response.HostId;
            await _settingsService.SaveAsync();

            _logger.LogInformation("Auto-registered as host {HostId}", response.HostId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-register as host");
        }
    }

    public async Task<List<ModelDownloadInfo>> GetRequiredDownloadsAsync()
    {
        try
        {
            await _settingsService.LoadAsync();
            await SyncUserModelPathAsync();

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
                bool needsDownload = !existingFiles.Contains(m.FileName);

                // Validate existing files — log warning but do not delete or re-download
                if (!needsDownload)
                {
                    var filePath = Path.Combine(modelPath, m.FileName);
                    var error = LocalModel.ValidateGgufFile(filePath);
                    if (error is not null)
                    {
                        _logger.LogWarning("Model '{Name}' failed validation ({Error}) — file kept on disk, skipping re-download", m.Name, error);
                    }
                }

                if (needsDownload)
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

    private async Task SyncUserModelPathAsync()
    {
        try
        {
            var userSettings = await _userSettingsService.GetSettingsAsync();
            if (!string.IsNullOrWhiteSpace(userSettings.ModelFolderPath))
            {
                _settingsService.Settings.Model.ModelFolderPath = userSettings.ModelFolderPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync user model folder path");
        }
    }

    public async Task<CreateInferenceResponse> CreateSessionAsync(CreateInferenceRequest request)
    {
        if (!_initialized)
            await InitializeAsync();

        DiagLog($"CreateSessionAsync: model={request.ModelName}, thinkLevel={request.ThinkLevel}");
        try
        {
            return await _inferenceService.CreateNewInferenceSessionAsync(request);
        }
        catch (Exception ex)
        {
            DiagLog($"CreateSessionAsync FAILED: {UnwrapException(ex)}");
            throw;
        }
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

    private static readonly string DiagLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DaisiHost", "tui-diag.log");

    private static void DiagLog(string message)
    {
        try
        {
            File.AppendAllText(DiagLogPath,
                $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private static string UnwrapException(Exception ex)
    {
        var msg = $"{ex.GetType().Name}: {ex.Message}\n  {ex.StackTrace}";
        var inner = ex.InnerException;
        while (inner is not null)
        {
            msg += $"\n  --- Inner: {inner.GetType().Name}: {inner.Message}\n  {inner.StackTrace}";
            inner = inner.InnerException;
        }
        return msg;
    }
}
