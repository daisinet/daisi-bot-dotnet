using Daisi.Host.Core.Services;
using Daisi.Host.Core.Services.Interfaces;
using Daisi.SDK.Models;
using DaisiBot.Agent.Auth;
using DaisiBot.Agent.Extensions;
using DaisiBot.Agent.Host;
using DaisiBot.Core.Interfaces;
using DaisiBot.Data;
using DaisiBot.Shared.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

using HostSettingsService = Daisi.Host.Core.Services.Interfaces.ISettingsService;

namespace DaisiBot.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

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

        // UI state
        builder.Services.AddSingleton<ChatNavigationState>();

        var app = builder.Build();

        // Ensure database created and initialize auth
        using (var scope = app.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DaisiBotDbContext>>();
            using var db = factory.CreateDbContext();
            db.Database.EnsureCreated();
        }

        var authService = app.Services.GetRequiredService<DaisiBotAuthService>();
        authService.InitializeAsync().GetAwaiter().GetResult();

        return app;
    }
}
