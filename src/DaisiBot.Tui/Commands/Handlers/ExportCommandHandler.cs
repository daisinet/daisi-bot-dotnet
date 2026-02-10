using System.Text;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Commands.Handlers;

public class ExportCommandHandler(IServiceProvider services, string context)
{
    public async Task<string?> HandleAsync(SlashCommand command)
    {
        var filename = command.Args.Length > 0
            ? command.RawArgs
            : $"export_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

        try
        {
            if (context == "bot")
            {
                var botStore = services.GetRequiredService<IBotStore>();
                var bots = await botStore.GetAllAsync();
                var sb = new StringBuilder();
                foreach (var bot in bots)
                {
                    sb.AppendLine($"=== {bot.Label} ({bot.Status}) ===");
                    var entries = await botStore.GetLogEntriesAsync(bot.Id);
                    foreach (var entry in entries)
                        sb.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Message}");
                    sb.AppendLine();
                }
                await File.WriteAllTextAsync(filename, sb.ToString());
            }
            else
            {
                var conversationStore = services.GetRequiredService<IConversationStore>();
                var conversations = await conversationStore.GetAllAsync();
                var sb = new StringBuilder();
                foreach (var conv in conversations)
                {
                    var full = await conversationStore.GetAsync(conv.Id);
                    if (full is null) continue;
                    sb.AppendLine($"=== {full.Title} ===");
                    foreach (var msg in full.Messages)
                        sb.AppendLine($"[{msg.Timestamp:yyyy-MM-dd HH:mm:ss}] [{msg.Role}] {msg.Content}");
                    sb.AppendLine();
                }
                await File.WriteAllTextAsync(filename, sb.ToString());
            }

            return $"Exported to {filename}";
        }
        catch (Exception ex)
        {
            return $"Export failed: {ex.Message}";
        }
    }
}
