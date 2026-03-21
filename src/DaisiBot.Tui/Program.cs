using Daisi.Host.Core.Services;
using Daisi.Host.Core.Services.Interfaces;
using Daisi.Inference.Interfaces;
using Daisi.Inference.LlamaSharp;
using Daisi.Inference.Models;
using Daisi.SDK.Models;
using DaisiBot.Agent.Auth;
using DaisiBot.Agent.Extensions;
using DaisiBot.Agent.Host;
using DaisiBot.Agent.Minion;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using DaisiBot.Data;
using DaisiBot.Tui;
using DaisiBot.Tui.Dialogs;
using DaisiBot.Tui.Screens;
using DaisiBot.Tui.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Velopack;

using HostSettingsService = Daisi.Host.Core.Services.Interfaces.ISettingsService;

// Parse minion CLI arguments
string? serverAddress = null;
bool headless = false;
string minionRole = "coder";
string? minionGoal = null;
string minionId = $"minion-{Guid.NewGuid():N}"[..16];

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--server" when i + 1 < args.Length:
            serverAddress = args[++i];
            if (!serverAddress.StartsWith("http://") && !serverAddress.StartsWith("https://"))
                serverAddress = $"http://{serverAddress}";
            break;
        case "--headless":
            headless = true;
            break;
        case "--role" when i + 1 < args.Length:
            minionRole = args[++i];
            break;
        case "--goal" when i + 1 < args.Length:
            minionGoal = args[++i];
            break;
        case "--id" when i + 1 < args.Length:
            minionId = args[++i];
            break;
    }
}

VelopackApp.Build()
    .OnAfterInstallFastCallback(v => VelopackInstallHooks.OnAfterInstall(v))
    .OnAfterUpdateFastCallback(v => VelopackInstallHooks.OnAfterUpdate(v))
    .OnBeforeUninstallFastCallback(v => VelopackInstallHooks.OnBeforeUninstall(v))
    .Run();

VelopackUpdateService.ShowVersionNumber();

// Diagnostic log helper for debugging TUI issues
var diagLogPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "DaisiHost", "tui-diag.log");
void DiagLog(string msg)
{
    try { File.AppendAllText(diagLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}"); } catch { }
}

try { File.WriteAllText(diagLogPath, ""); } catch { }
DiagLog("=== Program.cs starting ===");

// Capture unhandled exceptions for diagnostics
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    DiagLog($"UNHANDLED: {e.ExceptionObject}");
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    DiagLog($"UNOBSERVED TASK: {e.Exception}");
    e.SetObserved();
};

var builder = Host.CreateApplicationBuilder(args);

// Suppress all console logging — we own the terminal
builder.Logging.ClearProviders();

// Register LlamaSharp backend with log suppression
builder.Services.AddSingleton<ITextInferenceBackend>(sp =>
{
    DiagLog("Resolving ITextInferenceBackend (LlamaSharp)...");
    var backend = new LlamaSharpTextBackend();
    var config = new BackendConfiguration
    {
        LogCallback = (_, _) => { } // Suppress native logging — corrupts TUI
    };
    // Add app-local runtimes to native library search path (bundled in Velopack package)
    var appRuntimesPath = Path.Combine(AppContext.BaseDirectory, "runtimes");
    if (Directory.Exists(appRuntimesPath))
        config.SearchDirectories.Add(appRuntimesPath);
    backend.ConfigureAsync(config).GetAwaiter().GetResult();
    DiagLog("LlamaSharp backend configured");
    return backend;
});

// Configure DAISI network
DaisiStaticSettings.AutoswapOrc();

// SQLite
var dbPath = DaisiBotDbContext.GetDatabasePath();
builder.Services.AddDbContextFactory<DaisiBotDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Agent services
builder.Services.AddDaisiBotAgent();

// Host mode services (local inference) or remote minion inference
if (serverAddress is not null)
{
    // Remote minion mode: connect to summoner's gRPC server
    var remote = new RemoteInferenceService(
        serverAddress, minionId, minionRole, minionGoal ?? "",
        LoggerFactory.Create(b => { }).CreateLogger<RemoteInferenceService>());
    builder.Services.AddSingleton<ILocalInferenceService>(remote);
    builder.Services.AddSingleton(remote);

    // Still need host services for tool loading
    builder.Services.AddSingleton<HostSettingsService, DesktopSettingsService>();
    builder.Services.AddSingleton<ModelService>();
    builder.Services.AddSingleton<SkillSyncService>();
    builder.Services.AddSingleton<InferenceService>();
    builder.Services.AddSingleton<ToolService>();
}
else
{
    builder.Services.AddSingleton<HostSettingsService, DesktopSettingsService>();
    builder.Services.AddSingleton<ModelService>();
    builder.Services.AddSingleton<SkillSyncService>();
    builder.Services.AddSingleton<InferenceService>();
    builder.Services.AddSingleton<ToolService>();
    builder.Services.AddSingleton<ILocalInferenceService, LocalInferenceService>();
}

builder.Services.AddHttpClient();

var host = builder.Build();

DaisiStaticSettings.Services = host.Services;

// Ensure database created and schema up to date, then apply saved connection settings
using (var scope = host.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DaisiBotDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    await db.ApplyMigrationsAsync();

    var settingsService = scope.ServiceProvider.GetRequiredService<DaisiBot.Core.Interfaces.ISettingsService>();
    var settings = await settingsService.GetSettingsAsync();
    DaisiStaticSettings.ApplyUserSettings(settings.OrcDomain, settings.OrcPort, settings.OrcUseSsl);
#if DEBUG
    if (settings.LocalhostModeEnabled)
    {
        DaisiStaticSettings.ApplyUserSettings("localhost", 5001, true);
    }
#endif
}

// Initialize auth
var authService = host.Services.GetRequiredService<DaisiBotAuthService>();
authService.BotPlatform = "tui";
await authService.InitializeAsync();
#if DEBUG
{
    var settingsSvc = host.Services.GetRequiredService<DaisiBot.Core.Interfaces.ISettingsService>();
    var s = await settingsSvc.GetSettingsAsync();
    if (s.LocalhostModeEnabled)
        authService.AppId = "app-debug";
}
#endif

// Initialize remote inference if in minion mode
if (serverAddress is not null)
{
    var remote = host.Services.GetRequiredService<RemoteInferenceService>();
    await remote.InitializeAsync();
    DiagLog($"Remote inference connected to {serverAddress}");
}

DiagLog("Services initialized");

// Headless mode: run a goal autonomously without TUI
if (headless && minionGoal is not null)
{
    DiagLog($"Headless mode: role={minionRole}, goal={minionGoal}");
    var outputDir = Path.Combine(Directory.GetCurrentDirectory(), ".minion", minionId);
    Directory.CreateDirectory(outputDir);
    var outputPath = Path.Combine(outputDir, "output.log");

    var chatService = host.Services.GetRequiredService<IChatService>();
    var settingsSvc = host.Services.GetRequiredService<DaisiBot.Core.Interfaces.ISettingsService>();
    var settings = await settingsSvc.GetSettingsAsync();
    var conversationStore = host.Services.GetRequiredService<IConversationStore>();

    // Build system prompt with protocol instructions
    var protocolPrompt = MinionProtocol.GetMinionProtocolPrompt(minionId, minionRole);
    var systemPrompt = $"You are a {minionRole}. Your goal: {minionGoal}\n\n{protocolPrompt}";

    var conversation = new Conversation
    {
        ModelName = settings.DefaultModelName,
        ThinkLevel = settings.DefaultThinkLevel,
        SystemPrompt = systemPrompt
    };
    await conversationStore.CreateAsync(conversation);

    var config = new AgentConfig
    {
        ModelName = settings.DefaultModelName,
        ThinkLevel = settings.DefaultThinkLevel,
        Temperature = settings.Temperature,
        TopP = settings.TopP,
        MaxTokens = settings.MaxTokens,
        UseHostMode = true // Use local/remote inference
    };

    await File.WriteAllTextAsync(outputPath, $"[{DateTime.Now:O}] Minion {minionId} starting: {minionGoal}\n");

    try
    {
        await foreach (var chunk in chatService.SendMessageAsync(conversation.Id, minionGoal, config))
        {
            if (chunk.Type == "Text" || chunk.Type == "InferenceResponseTypesText")
            {
                await File.AppendAllTextAsync(outputPath, chunk.Content);
            }
        }
        await File.AppendAllTextAsync(outputPath, $"\n[{DateTime.Now:O}] Complete\n");

        // Write status file
        await File.WriteAllTextAsync(Path.Combine(outputDir, "status"), "complete");
    }
    catch (Exception ex)
    {
        await File.AppendAllTextAsync(outputPath, $"\n[{DateTime.Now:O}] Error: {ex.Message}\n");
        await File.WriteAllTextAsync(Path.Combine(outputDir, "status"), "failed");
    }

    return;
}

// Run TUI with raw console
DiagLog("Constructing TUI screens...");
var app = new App(host.Services);

try
{
    var chatScreen = new MainScreen(app);
    DiagLog("MainScreen created");
    var botScreen = new BotMainScreen(app);
    DiagLog("BotMainScreen created");
    var router = new ScreenRouter(app, chatScreen, botScreen);
    chatScreen.ScreenRouter = router;
    botScreen.ScreenRouter = router;

    // Restore last screen (default: bots)
    var lastScreen = "bots";
    UserSettings userSettings;
    {
        var settingsSvc = host.Services.GetRequiredService<DaisiBot.Core.Interfaces.ISettingsService>();
        userSettings = await settingsSvc.GetSettingsAsync();
        lastScreen = userSettings.LastScreen;
    }

    // Schedule model download check for first frame if self-host mode enabled (not localhost or DaisiNet)
    if (userSettings.HostModeEnabled && !userSettings.LocalhostModeEnabled)
    {
        app.Post(() =>
        {
            var dialog = new ModelDownloadDialog(app, host.Services);
            app.RunModal(dialog);
        });
    }

    DiagLog($"Starting event loop on screen: {lastScreen}, hostMode={userSettings.HostModeEnabled}, localhostMode={userSettings.LocalhostModeEnabled}");
    app.Run(router.GetScreen(lastScreen));
}
catch (Exception ex)
{
    DiagLog($"FATAL: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    throw;
}
