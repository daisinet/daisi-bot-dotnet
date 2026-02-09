using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;

namespace DaisiBot.Tui.Screens;

/// <summary>
/// Left sidebar showing conversation list with selection.
/// Renders within the bounds given by MainScreen.
/// </summary>
public class SidebarPanel
{
    private readonly App _app;
    private readonly IConversationStore _conversationStore;
    private List<Conversation> _conversations = [];
    private int _selectedIndex;
    private int _scrollOffset;

    public int Top { get; set; }
    public int Left { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool HasFocus { get; set; }

    public event Action<Conversation>? ConversationSelected;
    public event Action? NewConversationRequested;
    public event Action? DeleteConversationRequested;

    public Conversation? SelectedConversation =>
        _selectedIndex >= 0 && _selectedIndex < _conversations.Count
            ? _conversations[_selectedIndex]
            : null;

    public SidebarPanel(App app, IConversationStore conversationStore)
    {
        _app = app;
        _conversationStore = conversationStore;
    }

    public void LoadConversations()
    {
        Task.Run(async () =>
        {
            var conversations = await _conversationStore.GetAllAsync();
            _app.Post(() =>
            {
                _conversations = conversations;
                if (_selectedIndex >= _conversations.Count)
                    _selectedIndex = Math.Max(0, _conversations.Count - 1);
                Draw();
                AnsiConsole.Flush();
            });
        });
    }

    public void Draw()
    {
        // Title
        var titleBar = HasFocus ? " Conversations " : " Conversations ";
        AnsiConsole.WriteAt(Top, Left, "\u250C"); // ┌
        var titleLen = Math.Min(titleBar.Length, Width - 2);
        AnsiConsole.WriteAt(Top, Left + 1, new string('\u2500', Width - 2)); // ─
        AnsiConsole.WriteAt(Top, Left + Width - 1, "\u2510"); // ┐
        // Center title
        var titleStart = Left + (Width - titleLen) / 2;
        if (HasFocus)
        {
            AnsiConsole.SetBold();
            AnsiConsole.WriteAt(Top, titleStart, titleBar[..titleLen]);
            AnsiConsole.ResetStyle();
        }
        else
        {
            AnsiConsole.WriteAt(Top, titleStart, titleBar[..titleLen]);
        }

        // List area
        var listHeight = Height - 2; // minus top and bottom border
        var visibleCount = listHeight;

        // Ensure selected item is visible
        if (_selectedIndex < _scrollOffset)
            _scrollOffset = _selectedIndex;
        if (_selectedIndex >= _scrollOffset + visibleCount)
            _scrollOffset = _selectedIndex - visibleCount + 1;

        for (var i = 0; i < visibleCount; i++)
        {
            var row = Top + 1 + i;
            var idx = _scrollOffset + i;

            AnsiConsole.WriteAt(row, Left, "\u2502"); // │

            if (idx < _conversations.Count)
            {
                var conv = _conversations[idx];
                var title = conv.Title;
                var maxLen = Width - 3; // borders + padding
                if (title.Length > maxLen)
                    title = title[..(maxLen - 2)] + "..";

                var padded = title.PadRight(maxLen);

                if (idx == _selectedIndex)
                    AnsiConsole.WriteAtReverse(row, Left + 1, padded, Width - 2);
                else
                    AnsiConsole.WriteAt(row, Left + 1, padded, Width - 2);
            }
            else
            {
                AnsiConsole.WriteAt(row, Left + 1, new string(' ', Width - 2));
            }

            AnsiConsole.WriteAt(row, Left + Width - 1, "\u2502"); // │
        }

        // Bottom border
        var bottomRow = Top + Height - 1;
        AnsiConsole.WriteAt(bottomRow, Left, "\u2514"); // └
        AnsiConsole.WriteAt(bottomRow, Left + 1, new string('\u2500', Width - 2)); // ─
        AnsiConsole.WriteAt(bottomRow, Left + Width - 1, "\u2518"); // ┘

        // Hints at bottom
        var hints = " N:New D:Del ";
        if (hints.Length <= Width - 2)
        {
            var hintStart = Left + (Width - hints.Length) / 2;
            AnsiConsole.SetDim();
            AnsiConsole.WriteAt(bottomRow, hintStart, hints);
            AnsiConsole.ResetStyle();
        }
    }

    public bool HandleKey(ConsoleKeyInfo key)
    {
        if (!HasFocus) return false;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (_selectedIndex > 0)
                {
                    _selectedIndex--;
                    Draw();
                    NotifySelection();
                }
                return true;

            case ConsoleKey.DownArrow:
                if (_selectedIndex < _conversations.Count - 1)
                {
                    _selectedIndex++;
                    Draw();
                    NotifySelection();
                }
                return true;

            case ConsoleKey.Enter:
                NotifySelection();
                return true;

            case ConsoleKey.N:
                if (key.Modifiers == 0 || key.Modifiers == ConsoleModifiers.Shift)
                {
                    NewConversationRequested?.Invoke();
                    return true;
                }
                break;

            case ConsoleKey.D:
                if (key.Modifiers == 0)
                {
                    DeleteConversationRequested?.Invoke();
                    return true;
                }
                break;
        }

        return false;
    }

    private void NotifySelection()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _conversations.Count)
            ConversationSelected?.Invoke(_conversations[_selectedIndex]);
    }
}
