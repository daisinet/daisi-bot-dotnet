using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using DaisiBot.Tui.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Commands.Handlers;

public class NewCommandHandler(App app, IServiceProvider services, string context)
{
    public Task<string?> HandleAsync(SlashCommand command)
    {
        if (context == "bot")
        {
            app.Post(() =>
            {
                var flow = new BotCreationFlow(app, services, _ => { });
                app.RunModal(flow);
            });
        }
        else
        {
            // Create new conversation
            Task.Run(async () =>
            {
                var settingsService = services.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetSettingsAsync();
                var conversationStore = services.GetRequiredService<IConversationStore>();
                var conversation = new Conversation
                {
                    ModelName = settings.DefaultModelName,
                    ThinkLevel = settings.DefaultThinkLevel,
                    SystemPrompt = settings.SystemPrompt
                };
                await conversationStore.CreateAsync(conversation);
            });
            return Task.FromResult<string?>("New conversation created.");
        }
        return Task.FromResult<string?>(null);
    }
}
