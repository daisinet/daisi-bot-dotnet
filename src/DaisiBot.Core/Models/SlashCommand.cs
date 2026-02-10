namespace DaisiBot.Core.Models;

public record SlashCommand(string Name, string RawArgs, string[] Args);
