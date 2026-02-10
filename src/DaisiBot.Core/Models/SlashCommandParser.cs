namespace DaisiBot.Core.Models;

public static class SlashCommandParser
{
    public static bool IsSlashCommand(string input)
    {
        return !string.IsNullOrWhiteSpace(input) && input.TrimStart().StartsWith('/');
    }

    public static SlashCommand Parse(string input)
    {
        var trimmed = input.TrimStart();
        if (!trimmed.StartsWith('/'))
            throw new ArgumentException("Input is not a slash command", nameof(input));

        var parts = trimmed[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var name = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;
        var args = parts.Length > 1 ? parts[1..] : [];
        var rawArgs = parts.Length > 1 ? trimmed[(trimmed.IndexOf(' ') + 1)..].Trim() : string.Empty;

        return new SlashCommand(name, rawArgs, args);
    }
}
