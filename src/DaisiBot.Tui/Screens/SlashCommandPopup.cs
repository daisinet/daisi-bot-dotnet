namespace DaisiBot.Tui.Screens;

/// <summary>
/// Reusable inline autocomplete popup for slash commands.
/// Renders a filtered command list above the input row.
/// </summary>
public class SlashCommandPopup
{
    private record CommandEntry(string Name, string Description, int Priority, bool RequiresArgs = false);

    private static readonly CommandEntry[] AllCommands =
    [
        new("help",     "Show help",            1),
        new("new",      "New conversation/bot", 2),

        new("balance",  "Check credit balance", 4),
        new("status",   "Toggle status panel",  5),
        new("update",   "Edit bot settings",    6),
        new("start",    "Start bot",            7),
        new("stop",     "Stop bot",             8),
        new("kill",     "Stop & delete bot",    9),
        new("runnow",   "Run bot now",          10),
        new("clear",    "Clear output",         11),
        new("model",    "Pick model",           12),
        new("settings", "Open settings",        13),
        new("skills",   "Browse skills",        14),
        new("export",   "Export data",          15),
        new("install",  "Install skill",        16, RequiresArgs: true),
        new("login",    "Login flow",           17),
    ];

    private const int MaxVisible = 5;

    private List<CommandEntry> _filtered = [];
    private int _selectedIndex;

    public bool IsVisible { get; private set; }
    public int VisibleCount => _filtered.Count;
    public bool SelectedRequiresArgs =>
        IsVisible && _filtered.Count > 0 && _filtered[_selectedIndex].RequiresArgs;

    /// <summary>
    /// Update the filtered list based on current input buffer text.
    /// Shows popup if input starts with '/', hides otherwise.
    /// </summary>
    public void UpdateFilter(string input)
    {
        if (!input.StartsWith('/'))
        {
            Hide();
            return;
        }

        // Extract the command prefix typed after '/'
        // Only filter on the first token (stop at space)
        var afterSlash = input[1..];
        var spaceIdx = afterSlash.IndexOf(' ');
        if (spaceIdx >= 0)
        {
            // User has typed a space after the command name — hide popup
            Hide();
            return;
        }

        var filter = afterSlash.ToLowerInvariant();

        if (filter.Length == 0)
        {
            // Just '/' typed — show default top 5
            _filtered = AllCommands
                .OrderBy(c => c.Priority)
                .Take(MaxVisible)
                .ToList();
        }
        else
        {
            _filtered = AllCommands
                .Where(c => c.Name.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Priority)
                .Take(MaxVisible)
                .ToList();
        }

        if (_filtered.Count == 0)
        {
            Hide();
            return;
        }

        IsVisible = true;
        if (_selectedIndex >= _filtered.Count)
            _selectedIndex = _filtered.Count - 1;
    }

    /// <summary>
    /// Handle navigation and selection keys.
    /// Returns true if the key was consumed by the popup.
    /// </summary>
    public bool HandleKey(ConsoleKeyInfo key)
    {
        if (!IsVisible) return false;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (_selectedIndex > 0) _selectedIndex--;
                return true;

            case ConsoleKey.DownArrow:
                if (_selectedIndex < _filtered.Count - 1) _selectedIndex++;
                return true;

            case ConsoleKey.Tab:
            case ConsoleKey.Enter:
                // Completion will be retrieved by caller via GetCompletion()
                return true;

            case ConsoleKey.Escape:
                Hide();
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the full command text (e.g. "/help ") for the currently selected item,
    /// or null if nothing is selected / popup not visible.
    /// </summary>
    public string? GetCompletion()
    {
        if (!IsVisible || _filtered.Count == 0) return null;
        return $"/{_filtered[_selectedIndex].Name} ";
    }

    /// <summary>
    /// Draw the popup box above the input row.
    /// </summary>
    public void Draw(int inputRow, int left, int width)
    {
        if (!IsVisible || _filtered.Count == 0) return;

        var visibleCount = _filtered.Count;
        var height = visibleCount + 2; // +2 for top/bottom borders
        var top = inputRow - height;
        if (top < 0) top = 0;

        var contentWidth = width - 2;

        // Top border
        AnsiConsole.WriteAt(top, left, "\u250C");
        AnsiConsole.WriteAt(top, left + 1, new string('\u2500', contentWidth));
        AnsiConsole.WriteAt(top, left + width - 1, "\u2510");

        // Command rows
        for (var i = 0; i < visibleCount; i++)
        {
            var row = top + 1 + i;
            var entry = _filtered[i];
            var cmdText = $"/{entry.Name}";
            var desc = entry.Description;

            // Layout: │ /command    description │
            // Leave 1 char padding on each side inside borders
            var availableWidth = contentWidth;
            var gap = availableWidth - cmdText.Length - desc.Length;
            string lineContent;
            if (gap >= 2)
            {
                lineContent = cmdText + new string(' ', gap) + desc;
            }
            else
            {
                // Not enough room for description, just show command
                lineContent = cmdText.PadRight(availableWidth);
            }

            if (lineContent.Length > availableWidth)
                lineContent = lineContent[..availableWidth];
            else
                lineContent = lineContent.PadRight(availableWidth);

            AnsiConsole.WriteAt(row, left, "\u2502");
            if (i == _selectedIndex)
            {
                AnsiConsole.SetReverse();
                AnsiConsole.WriteAt(row, left + 1, lineContent);
                AnsiConsole.ResetStyle();
            }
            else
            {
                // Command in normal, description dimmed
                AnsiConsole.WriteAt(row, left + 1, cmdText);
                if (gap >= 2)
                {
                    AnsiConsole.SetDim();
                    AnsiConsole.WriteAt(row, left + 1 + cmdText.Length + gap, desc);
                    AnsiConsole.ResetStyle();
                }
            }
            AnsiConsole.WriteAt(row, left + width - 1, "\u2502");
        }

        // Bottom border
        var bottomRow = top + height - 1;
        AnsiConsole.WriteAt(bottomRow, left, "\u2514");
        AnsiConsole.WriteAt(bottomRow, left + 1, new string('\u2500', contentWidth));
        AnsiConsole.WriteAt(bottomRow, left + width - 1, "\u2518");
    }

    /// <summary>
    /// Clear the popup area (erase previous drawing).
    /// Call before redrawing when popup hides or item count changes.
    /// </summary>
    public void Clear(int inputRow, int left, int width, int previousVisibleCount)
    {
        if (previousVisibleCount <= 0) return;
        var height = previousVisibleCount + 2;
        var top = inputRow - height;
        if (top < 0) top = 0;
        AnsiConsole.ClearRegion(top, left, height, width);
    }

    public void Hide()
    {
        IsVisible = false;
        _selectedIndex = 0;
        _filtered = [];
    }
}
