using Daisi.Host.Core.Services;
using Daisi.Host.Core.Services.Interfaces;
using Daisi.Inference.Interfaces;
using Daisi.Inference.LlamaSharp;
using Daisi.Inference.Models;
using Daisi.SDK.Models;
using DaisiBot.Agent.Auth;
using DaisiBot.Agent.Extensions;
using DaisiBot.Agent.Host;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using DaisiBot.Data;
using DaisiBot.Tui;
using DaisiBot.Tui.Dialogs;
using DaisiBot.Tui.Screens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using HostSettingsService = Daisi.Host.Core.Services.Interfaces.ISettingsService;

var builder = Host.CreateApplicationBuilder(args);

// Suppress all console logging — we own the terminal
builder.Logging.ClearProviders();

// Register LlamaSharp backend with log suppression
builder.Services.AddSingleton<ITextInferenceBackend>(sp =>
{
    var backend = new LlamaSharpTextBackend();
    // Suppress LlamaSharp native logging — it writes directly to stdout and corrupts the TUI
    backend.ConfigureAsync(new BackendConfiguration
    {
        LogCallback = (_, _) => { }
    }).GetAwaiter().GetResult();
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

// Host mode services (local inference)
builder.Services.AddSingleton<HostSettingsService, DesktopSettingsService>();
builder.Services.AddSingleton<ModelService>();
builder.Services.AddSingleton<SkillSyncService>();
builder.Services.AddSingleton<InferenceService>();
builder.Services.AddSingleton<ToolService>();
builder.Services.AddSingleton<ILocalInferenceService, LocalInferenceService>();

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
await authService.InitializeAsync();
#if DEBUG
{
    var settingsSvc = host.Services.GetRequiredService<DaisiBot.Core.Interfaces.ISettingsService>();
    var s = await settingsSvc.GetSettingsAsync();
    if (s.LocalhostModeEnabled)
        authService.AppId = "app-debug";
}
#endif

// Run TUI with raw console
var app = new App(host.Services);
var chatScreen = new MainScreen(app);
var botScreen = new BotMainScreen(app);
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

app.Run(router.GetScreen(lastScreen));
