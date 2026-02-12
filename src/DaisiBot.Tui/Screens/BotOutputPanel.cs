using System.Text;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using DaisiBot.Tui.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Screens;

public class BotOutputPanel
{
    private readonly App _app;
    private readonly IServiceProvider _services;
    private BotInstance? _bot;

    // Input line editing
    private readonly StringBuilder _inputBuffer = new();
    private int _cursorPos;

    // Slash command autocomplete
    private readonly SlashCommandPopup _slashPopup = new();
    private int _lastPopupVisibleCount;

    // Output display
    private readonly List<string> _displayLines = [];
    private readonly List<bool> _isBoxBorder = [];
    private readonly List<BotLogLevel> _lineLevel = [];
    private readonly List<BotLogEntry> _logEntries = [];
    private int _scrollOffset;
    private int _lastWrapWidth;

    // Status
    private string _statusText = "";

    // Left padding inside the panel border
    private const int LeftPadding = 1;
    private static readonly string PadStr = new(' ', LeftPadding);

    public int Top { get; set; }
    public int Left { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool HasFocus { get; set; }

    public SlashCommandDispatcher? CommandDispatcher { get; set; }

    // Height breakdown: title(1) + output(H-4) + status(1) + input(1) + bottom_border(1)
    private int OutputAreaTop => Top + 1;
    private int OutputAreaHeight => Height - 4;
    private int StatusRow => Top + Height - 3;
    private int InputRow => Top + Height - 2;

    public BotOutputPanel(App app, IServiceProvider services)
    {
        _app = app;
        _services = services;
    }

    public void SetBot(BotInstance bot)
    {
        _bot = bot;
        _displayLines.Clear();
        _isBoxBorder.Clear();
        _lineLevel.Clear();
        _logEntries.Clear();
        _scrollOffset = 0;
        _statusText = $"{bot.Label} - {bot.Status}";

        // Load existing log entries
        Task.Run(async () =>
        {
            var botStore = _services.GetRequiredService<IBotStore>();
            var entries = await botStore.GetLogEntriesAsync(bot.Id);
            _app.Post(() =>
            {
                _logEntries.AddRange(entries);
                foreach (var entry in entries)
                    AddLogEntryLine(entry);
                _lastWrapWidth = Width;
                ScrollToEnd();
                Draw();
                AnsiConsole.Flush();
            });
        });
    }

    public void ClearBot()
    {
        _bot = null;
        _displayLines.Clear();
        _isBoxBorder.Clear();
        _lineLevel.Clear();
        _logEntries.Clear();
        _statusText = "";
        _scrollOffset = 0;
        Draw();
    }

    public void AppendLogEntry(BotLogEntry entry, bool skipDraw = false)
    {
        _logEntries.Add(entry);
        AddLogEntryLine(entry);
        ScrollToEnd();
        if (!skipDraw)
        {
            DrawOutput();
            AnsiConsole.Flush();
        }
    }

    private void RebuildDisplayLines()
    {
        _displayLines.Clear();
        _isBoxBorder.Clear();
        _lineLevel.Clear();
        foreach (var entry in _logEntries)
            AddLogEntryLine(entry);
        _lastWrapWidth = Width;
    }

    public void UpdateStatus(BotInstance bot)
    {
        _bot = bot;
        _statusText = $"{bot.Label} - {bot.Status}";
        if (bot.Status == BotStatus.WaitingForInput && bot.PendingQuestion is not null)
            _statusText = $"{bot.Label} - AWAITING INPUT";
        DrawStatusLine();
        AnsiConsole.Flush();
    }

    /// <summary>
    /// Reload log entries from the database and refresh the display.
    /// Called when BotStatusChanged fires to pick up new entries written during execution.
    /// </summary>
    public void RefreshLogEntries()
    {
        if (_bot is null) return;
        var botId = _bot.Id;
        Task.Run(async () =>
        {
            var botStore = _services.GetRequiredService<IBotStore>();
            var entries = await botStore.GetLogEntriesAsync(botId);
            _app.Post(() =>
            {
                if (_bot?.Id != botId) return; // bot changed while loading
                var prevCount = _logEntries.Count;
                if (entries.Count <= prevCount) return; // no new entries

                // Append only the new entries
                var newEntries = entries.Skip(prevCount).ToList();
                _logEntries.AddRange(newEntries);
                foreach (var entry in newEntries)
                    AddLogEntryLine(entry);
                ScrollToEnd();
                DrawOutput();
                AnsiConsole.Flush();
            });
        });
    }

    private void AddLogEntryLine(BotLogEntry entry)
    {
        var time = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        var stepLabel = "";
        if (entry.Level is BotLogLevel.StepStart or BotLogLevel.StepComplete
            && entry.Message.StartsWith("Step "))
        {
            var spaceIdx = entry.Message.IndexOf(' ', 5);
            if (spaceIdx < 0) spaceIdx = entry.Message.Length;
            var colonIdx = entry.Message.IndexOf(':', 5);
            if (colonIdx > 0 && colonIdx < spaceIdx) spaceIdx = colonIdx;
            stepLabel = " " + entry.Message[5..spaceIdx];
        }

        var prefix = entry.Level switch
        {
            BotLogLevel.Info => "[Bot]",
            BotLogLevel.StepStart => $"[Step{stepLabel}] \u25B6",
            BotLogLevel.StepComplete => $"[Step{stepLabel}] \u2713",
            BotLogLevel.Warning => "[Warn] \u26A0",
            BotLogLevel.Error => "[Error] \u2717",
            BotLogLevel.UserPrompt => "[Input Needed]",
            BotLogLevel.UserResponse => "[You]",
            BotLogLevel.SkillAction => "[Skill] \u26A1",
            _ => "[Info]"
        };

        var maxWidth = Width - 4 - LeftPadding;
        if (maxWidth <= 0) maxWidth = 40;

        var level = entry.Level;
        var isResult = level == BotLogLevel.StepComplete;

        if (isResult)
        {
            var borderWidth = maxWidth;
            _displayLines.Add("\u250C" + new string('\u2500', borderWidth - 2) + "\u2510");
            _isBoxBorder.Add(true);
            _lineLevel.Add(level);
        }

        var message = Sanitize(entry.Message);
        var headerLine = $"{prefix} {time} {message}";
        var headerLines = WordWrap(headerLine, maxWidth);
        foreach (var hl in headerLines)
        {
            _displayLines.Add(hl);
            _isBoxBorder.Add(false);
            _lineLevel.Add(level);
        }

        if (!string.IsNullOrWhiteSpace(entry.Detail))
        {
            var lines = WordWrap($"  {entry.Detail}", maxWidth);
            foreach (var dl in lines)
            {
                _displayLines.Add(dl);
                _isBoxBorder.Add(false);
                _lineLevel.Add(level);
            }
        }

        if (isResult)
        {
            var borderWidth = maxWidth;
            _displayLines.Add("\u2514" + new string('\u2500', borderWidth - 2) + "\u2518");
            _isBoxBorder.Add(true);
            _lineLevel.Add(level);
        }

        // Blank separator line between entries
        _displayLines.Add("");
        _isBoxBorder.Add(false);
        _lineLevel.Add(BotLogLevel.Info);
    }

    public void Draw()
    {
        // Re-wrap if width changed
        if (Width != _lastWrapWidth && _logEntries.Count > 0)
        {
            var atEnd = _scrollOffset >= Math.Max(0, _displayLines.Count - OutputAreaHeight);
            RebuildDisplayLines();
            if (atEnd) ScrollToEnd();
        }

        var contentWidth = Width - 2;

        // Title bar
        SetBorderColor();
        AnsiConsole.WriteAt(Top, Left, "\u250C");
        AnsiConsole.WriteAt(Top, Left + 1, new string('\u2500', contentWidth));
        AnsiConsole.WriteAt(Top, Left + Width - 1, "\u2510");
        ResetBorderColor();
        var title = _bot is not null
            ? $" {Truncate(_bot.Label, contentWidth - 4)} "
            : " Bot Output ";
        var titleStart = Left + (Width - Math.Min(title.Length, contentWidth)) / 2;
        if (HasFocus)
        {
            AnsiConsole.SetForeground(ConsoleColor.Green);
            AnsiConsole.SetBold();
            AnsiConsole.WriteAt(Top, titleStart, title, contentWidth);
            AnsiConsole.ResetStyle();
        }
        else
        {
            AnsiConsole.WriteAt(Top, titleStart, title, contentWidth);
        }

        // Output area
        DrawOutput();

        // Status line
        DrawStatusLine();

        // Input line
        DrawInputLine();

        // Slash command popup (above input line)
        _slashPopup.Draw(InputRow, Left, Width);

        // Bottom border
        var bottomRow = Top + Height - 1;
        SetBorderColor();
        AnsiConsole.WriteAt(bottomRow, Left, "\u2514");
        AnsiConsole.WriteAt(bottomRow, Left + 1, new string('\u2500', contentWidth));
        AnsiConsole.WriteAt(bottomRow, Left + Width - 1, "\u2518");
        ResetBorderColor();

        var hints = " Enter:Send  /:Commands  Esc:Stop ";
        if (hints.Length <= contentWidth)
        {
            var hintStart = Left + (Width - hints.Length) / 2;
            AnsiConsole.SetDim();
            AnsiConsole.WriteAt(bottomRow, hintStart, hints);
            AnsiConsole.ResetStyle();
        }

        // Position cursor at input
        if (HasFocus)
        {
            var cursorCol = Left + 3 + LeftPadding + _cursorPos; // "│" + pad + "> " prefix
            if (cursorCol < Left + Width - 1)
            {
                AnsiConsole.MoveTo(InputRow, cursorCol);
                AnsiConsole.ShowCursor();
            }
        }
        else
        {
            AnsiConsole.HideCursor();
        }
    }

    private void DrawOutput()
    {
        var innerWidth = Width - 2;
        var textWidth = innerWidth - LeftPadding;
        var areaHeight = OutputAreaHeight;

        if (_scrollOffset > Math.Max(0, _displayLines.Count - areaHeight))
            _scrollOffset = Math.Max(0, _displayLines.Count - areaHeight);

        for (var i = 0; i < areaHeight; i++)
        {
            var row = OutputAreaTop + i;
            var lineIdx = _scrollOffset + i;

            SetBorderColor();
            AnsiConsole.WriteAt(row, Left, "\u2502");
            ResetBorderColor();

            if (lineIdx < _displayLines.Count)
            {
                var line = Sanitize(_displayLines[lineIdx]);
                var isBorder = lineIdx < _isBoxBorder.Count && _isBoxBorder[lineIdx];
                var level = lineIdx < _lineLevel.Count ? _lineLevel[lineIdx] : BotLogLevel.Info;
                if (isBorder)
                {
                    AnsiConsole.SetForeground(ConsoleColor.Cyan);
                }
                else
                {
                    SetLevelColor(level);
                }
                AnsiConsole.WriteAt(row, Left + 1,
                    PadStr + Truncate(line, textWidth).PadRight(textWidth));
                AnsiConsole.ResetStyle();
            }
            else
            {
                AnsiConsole.WriteAt(row, Left + 1, new string(' ', innerWidth));
            }

            SetBorderColor();
            AnsiConsole.WriteAt(row, Left + Width - 1, "\u2502");
            ResetBorderColor();
        }
    }

    private void DrawStatusLine()
    {
        var innerWidth = Width - 2;
        var textWidth = innerWidth - LeftPadding;
        SetBorderColor();
        AnsiConsole.WriteAt(StatusRow, Left, "\u2502");
        ResetBorderColor();
        AnsiConsole.SetDim();
        AnsiConsole.SetForeground(ConsoleColor.Cyan);
        AnsiConsole.WriteAt(StatusRow, Left + 1,
            PadStr + Truncate(_statusText, textWidth).PadRight(textWidth));
        AnsiConsole.ResetStyle();
        SetBorderColor();
        AnsiConsole.WriteAt(StatusRow, Left + Width - 1, "\u2502");
        ResetBorderColor();
    }

    private void DrawInputLine()
    {
        var innerWidth = Width - 2;
        var textWidth = innerWidth - LeftPadding;
        SetBorderColor();
        AnsiConsole.WriteAt(InputRow, Left, "\u2502");
        ResetBorderColor();

        var prompt = "> ";
        var inputMaxLen = textWidth - prompt.Length;
        var displayInput = _inputBuffer.ToString();
        if (displayInput.Length > inputMaxLen)
            displayInput = displayInput[(displayInput.Length - inputMaxLen)..];

        AnsiConsole.WriteAt(InputRow, Left + 1, PadStr);
        AnsiConsole.SetForeground(ConsoleColor.Yellow);
        AnsiConsole.WriteAt(InputRow, Left + 1 + LeftPadding, prompt);
        AnsiConsole.ResetStyle();
        AnsiConsole.WriteAt(InputRow, Left + 1 + LeftPadding + prompt.Length,
            displayInput.PadRight(textWidth - prompt.Length));

        SetBorderColor();
        AnsiConsole.WriteAt(InputRow, Left + Width - 1, "\u2502");
        ResetBorderColor();

        // Reposition cursor
        if (HasFocus)
        {
            var cursorCol = Left + 3 + LeftPadding + _cursorPos;
            if (cursorCol < Left + Width - 1)
            {
                AnsiConsole.MoveTo(InputRow, cursorCol);
                AnsiConsole.ShowCursor();
            }
        }
    }

    private static void SetLevelColor(BotLogLevel level)
    {
        switch (level)
        {
            case BotLogLevel.Info:
                AnsiConsole.SetForeground(ConsoleColor.Green);
                break;
            case BotLogLevel.StepStart:
                AnsiConsole.SetForeground(ConsoleColor.Magenta);
                AnsiConsole.SetBold();
                break;
            case BotLogLevel.StepComplete:
                AnsiConsole.SetForeground(ConsoleColor.Cyan);
                AnsiConsole.SetBold();
                AnsiConsole.SetBackgroundRgb(0, 40, 50);
                break;
            case BotLogLevel.Warning:
                AnsiConsole.SetForeground(ConsoleColor.DarkYellow);
                AnsiConsole.SetBold();
                break;
            case BotLogLevel.Error:
                AnsiConsole.SetForeground(ConsoleColor.Red);
                AnsiConsole.SetBold();
                break;
            case BotLogLevel.UserPrompt:
                AnsiConsole.SetForeground(ConsoleColor.Yellow);
                AnsiConsole.SetBold();
                break;
            case BotLogLevel.UserResponse:
                AnsiConsole.SetForeground(ConsoleColor.Cyan);
                break;
            case BotLogLevel.SkillAction:
                AnsiConsole.SetForeground(ConsoleColor.Blue);
                AnsiConsole.SetBold();
                break;
        }
    }

    public bool HandleKey(ConsoleKeyInfo key)
    {
        if (!HasFocus) return false;

        // Slash command popup intercepts keys when visible
        if (_slashPopup.IsVisible)
        {
            if (_slashPopup.HandleKey(key))
            {
                if (key.Key is ConsoleKey.Tab or ConsoleKey.Enter)
                {
                    var requiresArgs = _slashPopup.SelectedRequiresArgs;
                    var completion = _slashPopup.GetCompletion();
                    if (completion is not null)
                    {
                        _inputBuffer.Clear();
                        _inputBuffer.Append(completion);
                        _cursorPos = _inputBuffer.Length;
                    }
                    _slashPopup.Clear(InputRow, Left, Width, _lastPopupVisibleCount);
                    _slashPopup.Hide();
                    _lastPopupVisibleCount = 0;
                    DrawOutput(); // repaint area the popup covered
                    DrawInputLine();
                    AnsiConsole.Flush();

                    if (!requiresArgs)
                        ProcessInput();
                }
                else
                {
                    // Up/Down/Esc — just redraw popup
                    RedrawPopupArea();
                    AnsiConsole.Flush();
                }
                return true;
            }
        }

        switch (key.Key)
        {
            case ConsoleKey.Enter:
                ProcessInput();
                return true;

            case ConsoleKey.Escape:
                StopBot();
                return true;

            case ConsoleKey.Backspace:
                if (_cursorPos > 0)
                {
                    _inputBuffer.Remove(_cursorPos - 1, 1);
                    _cursorPos--;
                    OnInputChanged();
                }
                return true;

            case ConsoleKey.Delete:
                if (_cursorPos < _inputBuffer.Length)
                {
                    _inputBuffer.Remove(_cursorPos, 1);
                    OnInputChanged();
                }
                return true;

            case ConsoleKey.LeftArrow:
                if (_cursorPos > 0) { _cursorPos--; DrawInputLine(); }
                return true;

            case ConsoleKey.RightArrow:
                if (_cursorPos < _inputBuffer.Length) { _cursorPos++; DrawInputLine(); }
                return true;

            case ConsoleKey.Home:
                _cursorPos = 0;
                DrawInputLine();
                return true;

            case ConsoleKey.End:
                _cursorPos = _inputBuffer.Length;
                DrawInputLine();
                return true;

            case ConsoleKey.PageUp:
                _scrollOffset = Math.Max(0, _scrollOffset - OutputAreaHeight);
                DrawOutput();
                return true;

            case ConsoleKey.PageDown:
                _scrollOffset = Math.Min(
                    Math.Max(0, _displayLines.Count - OutputAreaHeight),
                    _scrollOffset + OutputAreaHeight);
                DrawOutput();
                return true;

            default:
                if (key.KeyChar >= 32 && key.KeyChar < 127)
                {
                    _inputBuffer.Insert(_cursorPos, key.KeyChar);
                    _cursorPos++;
                    OnInputChanged();
                    return true;
                }
                break;
        }

        return false;
    }

    private void OnInputChanged()
    {
        var prevCount = _lastPopupVisibleCount;
        _slashPopup.UpdateFilter(_inputBuffer.ToString());

        if (!_slashPopup.IsVisible && prevCount > 0)
        {
            _slashPopup.Clear(InputRow, Left, Width, prevCount);
            _lastPopupVisibleCount = 0;
            DrawOutput();
        }
        else if (_slashPopup.IsVisible)
        {
            if (prevCount > 0)
                _slashPopup.Clear(InputRow, Left, Width, prevCount);
            _slashPopup.Draw(InputRow, Left, Width);
            _lastPopupVisibleCount = _slashPopup.VisibleCount;
        }

        DrawInputLine();
        AnsiConsole.Flush();
    }

    private void RedrawPopupArea()
    {
        if (_slashPopup.IsVisible)
        {
            if (_lastPopupVisibleCount > 0)
                _slashPopup.Clear(InputRow, Left, Width, _lastPopupVisibleCount);
            _slashPopup.Draw(InputRow, Left, Width);
            _lastPopupVisibleCount = _slashPopup.VisibleCount;
        }
        else if (_lastPopupVisibleCount > 0)
        {
            _slashPopup.Clear(InputRow, Left, Width, _lastPopupVisibleCount);
            _lastPopupVisibleCount = 0;
            DrawOutput();
        }
    }

    private void ProcessInput()
    {
        var input = _inputBuffer.ToString().Trim();
        if (string.IsNullOrWhiteSpace(input)) return;

        // Dismiss popup if open
        if (_slashPopup.IsVisible || _lastPopupVisibleCount > 0)
        {
            _slashPopup.Clear(InputRow, Left, Width, _lastPopupVisibleCount);
            _slashPopup.Hide();
            _lastPopupVisibleCount = 0;
        }

        _inputBuffer.Clear();
        _cursorPos = 0;

        if (SlashCommandParser.IsSlashCommand(input))
        {
            var cmd = SlashCommandParser.Parse(input);
            if (CommandDispatcher is not null)
            {
                Task.Run(async () =>
                {
                    var result = await CommandDispatcher.DispatchAsync(cmd);
                    if (result is not null)
                    {
                        _app.Post(() =>
                        {
                            if (result == "__CLEAR__")
                            {
                                _displayLines.Clear();
                                _isBoxBorder.Clear();
                                _lineLevel.Clear();
                                _scrollOffset = 0;
                            }
                            else
                            {
                                _displayLines.Add($"[System] {result}");
                                _isBoxBorder.Add(false);
                                _lineLevel.Add(BotLogLevel.Info);
                                ScrollToEnd();
                            }
                            Draw();
                            AnsiConsole.Flush();
                        });
                    }
                });
            }
        }
        else if (_bot is not null)
        {
            var botEngine = _services.GetRequiredService<IBotEngine>();
            if (botEngine.IsRunning(_bot.Id))
            {
                // Queue instruction for the next execution cycle
                _displayLines.Add($"[You] {DateTime.Now:HH:mm:ss} {input}");
                _isBoxBorder.Add(false);
                _lineLevel.Add(BotLogLevel.UserResponse);
                ScrollToEnd();
                Draw();
                AnsiConsole.Flush();

                Task.Run(async () => await botEngine.SendInputAsync(_bot.Id, input));
            }
            else
            {
                _displayLines.Add($"[System] Bot is not running. Use /start to start it.");
                _isBoxBorder.Add(false);
                _lineLevel.Add(BotLogLevel.Info);
                ScrollToEnd();
                Draw();
                AnsiConsole.Flush();
            }
        }

        DrawInputLine();
    }

    private void StopBot()
    {
        if (_bot is null) return;
        var botEngine = _services.GetRequiredService<IBotEngine>();
        Task.Run(async () => await botEngine.StopBotAsync(_bot.Id));
    }

    public void AddSystemMessage(string message)
    {
        _displayLines.Add($"[System] {message}");
        _isBoxBorder.Add(false);
        _lineLevel.Add(BotLogLevel.Info);
        ScrollToEnd();
    }

    public void ClearOutput()
    {
        _displayLines.Clear();
        _isBoxBorder.Clear();
        _lineLevel.Clear();
        _scrollOffset = 0;
        Draw();
    }

    private void ScrollToEnd()
    {
        _scrollOffset = Math.Max(0, _displayLines.Count - OutputAreaHeight);
    }

    private void SetBorderColor()
    {
        if (HasFocus) AnsiConsole.SetForeground(ConsoleColor.Green);
    }

    private void ResetBorderColor()
    {
        if (HasFocus) AnsiConsole.ResetStyle();
    }

    private static string Sanitize(string s) =>
        s.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 2)] + "..";

    private static List<string> WordWrap(string text, int maxWidth)
    {
        if (maxWidth <= 0) maxWidth = 40;
        var result = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            result.Add("");
            return result;
        }

        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            var clean = line.TrimEnd('\r');
            if (clean.Length == 0) { result.Add(""); continue; }

            var remaining = clean;
            while (remaining.Length > maxWidth)
            {
                var breakPos = remaining.LastIndexOf(' ', maxWidth);
                if (breakPos <= 0) breakPos = maxWidth;
                result.Add(remaining[..breakPos]);
                remaining = remaining[breakPos..].TrimStart();
            }
            if (remaining.Length > 0) result.Add(remaining);
        }
        return result;
    }
}
