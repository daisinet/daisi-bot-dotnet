using System.Text.RegularExpressions;

namespace DaisiBot.Agent.Processing;

public static partial class ContentCleaner
{
    private static readonly string[] AntiPrompts = ["User:", "User:\n", "\n\n\n", "###"];

    public static string Clean(string rawContent)
    {
        if (string.IsNullOrEmpty(rawContent))
            return string.Empty;

        var cleaned = ThinkTagRegex().Replace(rawContent, "");
        cleaned = cleaned.Replace("<response>", "").Replace("</response>", "");

        cleaned = cleaned.TrimEnd();
        foreach (var antiPrompt in AntiPrompts)
        {
            var trimmed = antiPrompt.Trim();
            if (!string.IsNullOrEmpty(trimmed) && cleaned.EndsWith(trimmed, StringComparison.Ordinal))
            {
                cleaned = cleaned[..^trimmed.Length].TrimEnd();
            }
        }

        return cleaned;
    }

    [GeneratedRegex(@"<think>[\s\S]*?</think>", RegexOptions.Compiled)]
    private static partial Regex ThinkTagRegex();
}
