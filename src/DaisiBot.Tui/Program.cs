using Daisi.Host.Core.Services;
using Daisi.Host.Core.Services.Interfaces;
using Daisi.SDK.Models;
using DaisiBot.Agent.Auth;
using DaisiBot.Agent.Extensions;
using DaisiBot.Agent.Host;
using DaisiBot.Core.Interfaces;
using DaisiBot.Data;
using DaisiBot.Tui;
using DaisiBot.Tui.Screens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using HostSettingsService = Daisi.Host.Core.Services.Interfaces.ISettingsService;

var builder = Host.CreateApplicationBuilder(args);

// Suppress all console logging — we own the terminal
builder.Logging.ClearProviders();

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
builder.Services.AddSingleton<InferenceService>();
builder.Services.AddSingleton<ToolService>();
builder.Services.AddSingleton<ILocalInferenceService, LocalInferenceService>();

var host = builder.Build();

// Ensure database created
using (var scope = host.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DaisiBotDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}

// Initialize auth
var authService = host.Services.GetRequiredService<DaisiBotAuthService>();
await authService.InitializeAsync();

// Run TUI with raw console
var app = new App(host.Services);
var mainScreen = new MainScreen(app);
app.Run(mainScreen);
