using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;

namespace DaisiBot.Tui.Screens;

public class BotSidebarPanel
{
    private readonly App _app;
    private readonly IBotStore _botStore;
    private List<BotInstance> _bots = [];
    private int _selectedIndex;
    private int _scrollOffset;

    // Flashing state for WaitingForInput bots
    private readonly HashSet<Guid> _flashingBots = [];
    private bool _flashOn;
    private readonly System.Threading.Timer _flashTimer;

    public int Top { get; set; }
    public int Left { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool HasFocus { get; set; }

    /// <summary>Set by the owning screen so the panel can skip draws when not visible.</summary>
    public Func<bool>? IsScreenActive { get; set; }

    public event Action<BotInstance>? BotSelected;
    public event Action? NewBotRequested;
    public event Action? DeleteBotRequested;

    public BotInstance? SelectedBot =>
        _selectedIndex >= 0 && _selectedIndex < _bots.Count
            ? _bots[_selectedIndex]
            : null;

    public BotSidebarPanel(App app, IBotStore botStore)
    {
        _app = app;
        _botStore = botStore;
        _flashTimer = new System.Threading.Timer(OnFlashTick, null, 500, 500);
    }

    private void OnFlashTick(object? state)
    {
        if (_flashingBots.Count == 0) return;
        _flashOn = !_flashOn;
        if (IsScreenActive?.Invoke() != true) return;
        _app.Post(() =>
        {
            Draw();
            AnsiConsole.Flush();
        });
    }

    public void UpdateFlashingBots(BotInstance bot)
    {
        if (bot.Status == BotStatus.WaitingForInput)
            _flashingBots.Add(bot.Id);
        else
            _flashingBots.Remove(bot.Id);
    }

    public void LoadBots(Action? onLoaded = null, bool skipDraw = false)
    {
        Task.Run(async () =>
        {
            var bots = await _botStore.GetAllAsync();
            _app.Post(() =>
            {
                _bots = bots;
                if (_selectedIndex >= _bots.Count)
                    _selectedIndex = Math.Max(0, _bots.Count - 1);

                _flashingBots.Clear();
                foreach (var b in _bots)
                {
                    if (b.Status == BotStatus.WaitingForInput)
                        _flashingBots.Add(b.Id);
                }

                if (!skipDraw)
                {
                    Draw();
                    AnsiConsole.Flush();
                }
                onLoaded?.Invoke();
            });
        });
    }

    public void Draw()
    {
        var contentWidth = Width - 2;

        // Title
        SetBorderColor();
        AnsiConsole.WriteAt(Top, Left, "\u250C");
        AnsiConsole.WriteAt(Top, Left + 1, new string('\u2500', contentWidth));
        AnsiConsole.WriteAt(Top, Left + Width - 1, "\u2510");
        ResetBorderColor();
        var titleBar = " Bots ";
        var titleStart = Left + (Width - titleBar.Length) / 2;
        if (HasFocus)
        {
            AnsiConsole.SetForeground(ConsoleColor.Green);
            AnsiConsole.SetBold();
            AnsiConsole.WriteAt(Top, titleStart, titleBar);
            AnsiConsole.ResetStyle();
        }
        else
        {
            AnsiConsole.WriteAt(Top, titleStart, titleBar);
        }

        // List area
        var listHeight = Height - 2;
        var visibleCount = listHeight;

        if (_selectedIndex < _scrollOffset)
            _scrollOffset = _selectedIndex;
        if (_selectedIndex >= _scrollOffset + visibleCount)
            _scrollOffset = _selectedIndex - visibleCount + 1;

        for (var i = 0; i < visibleCount; i++)
        {
            var row = Top + 1 + i;
            var idx = _scrollOffset + i;

            SetBorderColor();
            AnsiConsole.WriteAt(row, Left, "\u2502");
            ResetBorderColor();

            if (idx < _bots.Count)
            {
                var bot = _bots[idx];
                var icon = GetStatusIcon(bot);
                var label = bot.Label;
                var maxLen = contentWidth - 3;
                if (label.Length > maxLen)
                    label = label[..(maxLen - 2)] + "..";

                var line = $"{icon} {label}";
                var padded = line.PadRight(contentWidth);

                if (idx == _selectedIndex)
                {
                    AnsiConsole.WriteAtReverse(row, Left + 1, padded, contentWidth);
                }
                else
                {
                    SetStatusColor(bot);
                    AnsiConsole.WriteAt(row, Left + 1, padded, contentWidth);
                    AnsiConsole.ResetStyle();
                }
            }
            else
            {
                AnsiConsole.WriteAt(row, Left + 1, new string(' ', contentWidth));
            }

            SetBorderColor();
            AnsiConsole.WriteAt(row, Left + Width - 1, "\u2502");
            ResetBorderColor();
        }

        // Bottom border
        var bottomRow = Top + Height - 1;
        SetBorderColor();
        AnsiConsole.WriteAt(bottomRow, Left, "\u2514");
        AnsiConsole.WriteAt(bottomRow, Left + 1, new string('\u2500', contentWidth));
        AnsiConsole.WriteAt(bottomRow, Left + Width - 1, "\u2518");
        ResetBorderColor();

        var hints = " N:New D:Del ";
        if (hints.Length <= contentWidth)
        {
            var hintStart = Left + (Width - hints.Length) / 2;
            AnsiConsole.SetDim();
            AnsiConsole.WriteAt(bottomRow, hintStart, hints);
            AnsiConsole.ResetStyle();
        }
    }

    private void SetBorderColor()
    {
        if (HasFocus) AnsiConsole.SetForeground(ConsoleColor.Green);
    }

    private void ResetBorderColor()
    {
        if (HasFocus) AnsiConsole.ResetStyle();
    }

    private string GetStatusIcon(BotInstance bot)
    {
        return bot.Status switch
        {
            BotStatus.Running => "\u25B6",         // ▶
            BotStatus.Idle => "\u25CB",             // ○
            BotStatus.WaitingForInput => _flashOn ? "\u26A0" : " ", // ⚠ flashing
            BotStatus.Completed => "\u2713",        // ✓
            BotStatus.Failed => "\u2717",           // ✗
            BotStatus.Stopped => "\u25A0",          // ■
            _ => " "
        };
    }

    private void SetStatusColor(BotInstance bot)
    {
        switch (bot.Status)
        {
            case BotStatus.Running:
                AnsiConsole.SetForeground(ConsoleColor.Green);
                break;
            case BotStatus.Idle:
                AnsiConsole.SetForeground(ConsoleColor.Gray);
                break;
            case BotStatus.WaitingForInput:
                AnsiConsole.SetForeground(ConsoleColor.Yellow);
                if (_flashOn) AnsiConsole.SetBold();
                break;
            case BotStatus.Failed:
                AnsiConsole.SetForeground(ConsoleColor.Red);
                break;
            case BotStatus.Stopped:
                AnsiConsole.SetForeground(ConsoleColor.DarkRed);
                break;
            case BotStatus.Completed:
                AnsiConsole.SetForeground(ConsoleColor.Cyan);
                break;
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
                if (_selectedIndex < _bots.Count - 1)
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
                    NewBotRequested?.Invoke();
                    return true;
                }
                break;

            case ConsoleKey.D:
                if (key.Modifiers == 0)
                {
                    DeleteBotRequested?.Invoke();
                    return true;
                }
                break;
        }

        return false;
    }

    public bool HasItems => _bots.Count > 0;

    public void SelectFirst()
    {
        if (_bots.Count > 0)
        {
            _selectedIndex = 0;
            _scrollOffset = 0;
            NotifySelection();
        }
    }

    private void NotifySelection()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _bots.Count)
            BotSelected?.Invoke(_bots[_selectedIndex]);
    }
}
