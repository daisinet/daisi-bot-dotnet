using Daisi.Host.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DaisiBot.Agent.Minion;

/// <summary>
/// Hosts a gRPC inference server within the existing process using Kestrel.
/// Started by /summon, stopped by /unsummon.
/// </summary>
public sealed class MinionInferenceServer : IAsyncDisposable
{
    private readonly InferenceService _inferenceService;
    private readonly ILoggerFactory _loggerFactory;
    private WebApplication? _app;
    private MinionSessionManager? _sessionManager;
    private int _port;

    public bool IsRunning => _app is not null;
    public int Port => _port;
    public MinionSessionManager? SessionManager => _sessionManager;

    public MinionInferenceServer(InferenceService inferenceService, ILoggerFactory loggerFactory)
    {
        _inferenceService = inferenceService;
        _loggerFactory = loggerFactory;
    }

    public async Task StartAsync(int port = 50051)
    {
        if (_app is not null)
            throw new InvalidOperationException("Server is already running");

        _port = port;
        _sessionManager = new MinionSessionManager(
            _inferenceService,
            _loggerFactory.CreateLogger<MinionSessionManager>());
        _sessionManager.Start();

        var builder = WebApplication.CreateBuilder();

        // Suppress all console logging — we own the terminal
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton(_sessionManager);
        builder.Services.AddSingleton(_loggerFactory);
        builder.Services.AddGrpc();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(port, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });

        _app = builder.Build();
        _app.MapGrpcService<MinionInferenceGrpcService>();

        await _app.StartAsync();

        _loggerFactory.CreateLogger<MinionInferenceServer>()
            .LogInformation("Minion inference server started on localhost:{Port}", port);
    }

    public async Task StopAsync()
    {
        if (_sessionManager is not null)
        {
            await _sessionManager.StopAsync();
            _sessionManager.Dispose();
            _sessionManager = null;
        }

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }

        _loggerFactory.CreateLogger<MinionInferenceServer>()
            .LogInformation("Minion inference server stopped");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
