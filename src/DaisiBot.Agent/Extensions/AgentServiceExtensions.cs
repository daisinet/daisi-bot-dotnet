using Daisi.SDK.Extensions;
using Daisi.SDK.Interfaces.Authentication;
using DaisiBot.Agent.Auth;
using DaisiBot.Agent.Chat;
using DaisiBot.Agent.Models;
using DaisiBot.Core.Interfaces;
using DaisiBot.Data.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Agent.Extensions;

public static class AgentServiceExtensions
{
    public static IServiceCollection AddDaisiBotAgent(this IServiceCollection services)
    {
        // Key provider
        services.AddSingleton<DaisiBotClientKeyProvider>();
        services.AddSingleton<IClientKeyProvider>(sp => sp.GetRequiredService<DaisiBotClientKeyProvider>());

        // DAISI SDK clients
        services.AddDaisiClients();

        // Auth
        services.AddSingleton<SqliteAuthStateStore>();
        services.AddSingleton<DaisiBotAuthService>();
        services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<DaisiBotAuthService>());

        // Chat
        services.AddTransient<IChatService, DaisiBotChatService>();

        // Models
        services.AddTransient<IModelService, DaisiBotModelService>();

        // Data stores
        services.AddTransient<IConversationStore, SqliteConversationStore>();
        services.AddTransient<ISettingsService, SqliteSettingsService>();
        services.AddTransient<SqliteInstalledSkillStore>();

        return services;
    }
}
