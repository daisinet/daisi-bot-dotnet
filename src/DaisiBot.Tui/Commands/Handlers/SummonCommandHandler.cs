using Daisi.Host.Core.Hardware;
using Daisi.Host.Core.Services;
using Daisi.Llogos.Chat;
using DaisiBot.Agent.Minion;
using DaisiBot.Agent.Minion.Tools;
using DaisiBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DaisiBot.Tui.Commands.Handlers;

public class SummonCommandHandler
{
    private readonly IServiceProvider _services;
    private readonly App _app;

    // Centralized mode (existing)
    private MinionInferenceServer? _server;
    private MinionProcessManager? _processManager;

    // Distributed mode (new)
    private DistributedMinionManager? _distributedManager;

    public MinionInferenceServer? Server => _server;
    public MinionProcessManager? ProcessManager => _processManager;
    public DistributedMinionManager? DistributedManager => _distributedManager;

    public bool IsSummoned => _server?.IsRunning == true || _distributedManager is not null;
    public bool IsDistributed => _distributedManager is not null;
    public int ServerPort => _server?.Port ?? 0;

    public SummonCommandHandler(App app, IServiceProvider services)
    {
        _app = app;
        _services = services;
    }

    public async Task<string?> HandleSummonAsync(SlashCommand command)
    {
        if (IsSummoned)
        {
            if (IsDistributed)
                return $"Already summoned (distributed). {_distributedManager!.Budget.Summary}. Use /unsummon to stop.";
            return $"Already summoned. Inference server running on localhost:{_server!.Port}. Use /unsummon to stop.";
        }

        // Parse --centralized flag
        bool centralized = command.Args.Any(a =>
            a.Equals("--centralized", StringComparison.OrdinalIgnoreCase));
        var positionalArgs = command.Args.Where(a => !a.StartsWith("--")).ToArray();

        if (centralized)
            return await HandleCentralizedSummonAsync(positionalArgs);

        return await HandleDistributedSummonAsync();
    }

    private async Task<string?> HandleDistributedSummonAsync()
    {
        // Get the model service to find the loaded llogos model handle
        var modelService = _services.GetService<ModelService>();
        if (modelService is null)
            return "Cannot summon: no model service available. Enable host mode first.";

        // Find the loaded DaisiLlogos model handle
        DaisiLlogosModelHandle? modelHandle = null;
        foreach (var localModel in modelService.LocalModels)
        {
            if (localModel.ModelHandle is DaisiLlogosModelHandleAdapter adapter && adapter.Inner.IsLoaded)
            {
                modelHandle = adapter.Inner;
                break;
            }
        }

        if (modelHandle is null)
            return "Cannot summon (distributed): no DaisiLlogos model loaded. " +
                   "Load a model first, or use /summon --centralized for the gRPC mode.";

        // Query free VRAM
        long? freeVram = GpuVramDetector.GetPrimaryGpuFreeVramBytes();
        if (freeVram is null or 0)
            return "Cannot summon (distributed): no GPU detected. " +
                   "Use /summon --centralized for CPU/gRPC mode.";

        // Calculate budget
        var budget = MinionVramBudget.Calculate(
            modelHandle.Config,
            freeVram.Value,
            modelHandle.ContextSize);

        if (budget.MaxMinions < 1)
            return $"Cannot summon (distributed): {budget.Summary}";

        // Create gate and manager
        var gate = new GpuInferenceGate();
        _distributedManager = new DistributedMinionManager(modelHandle, gate, budget);

        return $"Summoned (distributed)! {budget.Summary}\n" +
               "Use /spawn <role> <goal> to create worker minions.";
    }

    private async Task<string?> HandleCentralizedSummonAsync(string[] args)
    {
        var inferenceService = _services.GetService<InferenceService>();
        if (inferenceService is null)
            return "Cannot summon: no local inference service available. Enable host mode first.";

        var loggerFactory = _services.GetRequiredService<ILoggerFactory>();
        _server = new MinionInferenceServer(inferenceService, loggerFactory);

        var port = 50051;
        if (args.Length > 0 && int.TryParse(args[0], out var customPort))
            port = customPort;

        try
        {
            await _server.StartAsync(port);
            _processManager = new MinionProcessManager(loggerFactory.CreateLogger<MinionProcessManager>());

            return $"Summoned (centralized)! gRPC inference server running on localhost:{port}. " +
                   "Use spawn_minion tool or /spawn to create worker minions.";
        }
        catch (Exception ex)
        {
            _server = null;
            return $"Failed to summon: {ex.Message}";
        }
    }

    public async Task<string?> HandleUnsummonAsync(SlashCommand command)
    {
        if (!IsSummoned)
            return "Not summoned. Nothing to unsummon.";

        try
        {
            if (_distributedManager is not null)
            {
                await _distributedManager.DisposeAsync();
                _distributedManager = null;
                return "Unsummoned. All distributed minion sessions disposed, VRAM freed.";
            }

            // Centralized mode cleanup
            _processManager?.StopAll();
            _processManager?.Dispose();
            _processManager = null;

            await _server!.StopAsync();
            _server = null;

            return "Unsummoned. gRPC server stopped, all minions killed, sessions closed.";
        }
        catch (Exception ex)
        {
            return $"Error during unsummon: {ex.Message}";
        }
    }
}
