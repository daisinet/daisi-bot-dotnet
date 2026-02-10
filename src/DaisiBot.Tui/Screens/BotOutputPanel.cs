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
    private int _scrollOffset;

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
        _scrollOffset = 0;
        _statusText = $"{bot.Label} - {bot.Status}";

        // Load existing log entries
        Task.Run(async () =>
        {
            var botStore = _services.GetRequiredService<IBotStore>();
            var entries = await botStore.GetLogEntriesAsync(bot.Id);
            _app.Post(() =>
            {
                foreach (var entry in entries)
                    AddLogEntryLine(entry);
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
        _statusText = "";
        _scrollOffset = 0;
        Draw();
    }

    public void AppendLogEntry(BotLogEntry entry)
    {
        AddLogEntryLine(entry);
        ScrollToEnd();
        DrawOutput();
        AnsiConsole.Flush();
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

    private void AddLogEntryLine(BotLogEntry entry)
    {
        var time = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        var prefix = entry.Level switch
        {
            BotLogLevel.Info => "[Bot]",
            BotLogLevel.StepStart => "[Step] \u25B6",
            BotLogLevel.StepComplete => "[Result] \u2713",
            BotLogLevel.Warning => "[Warn] \u26A0",
            BotLogLevel.Error => "[Error] \u2717",
            BotLogLevel.UserPrompt => "[Input Needed]",
            BotLogLevel.UserResponse => "[You]",
            BotLogLevel.SkillAction => "[Skill] \u26A1",
            _ => "[Info]"
        };

        var message = Sanitize(entry.Message);
        _displayLines.Add($"{prefix} {time} {message}");

        if (!string.IsNullOrWhiteSpace(entry.Detail))
        {
            var maxWidth = Width - 6;
            if (maxWidth <= 0) maxWidth = 40;
            var lines = WordWrap($"  {entry.Detail}", maxWidth);
            _displayLines.AddRange(lines);
        }
    }

    public void Draw()
    {
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
                SetLineColor(line);
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

    private static void SetLineColor(string line)
    {
        if (line.StartsWith("[Bot]"))
            AnsiConsole.SetForeground(ConsoleColor.Green);
        else if (line.StartsWith("[Error]"))
        {
            AnsiConsole.SetForeground(ConsoleColor.Red);
            AnsiConsole.SetBold();
        }
        else if (line.StartsWith("[Step]"))
        {
            AnsiConsole.SetForeground(ConsoleColor.Magenta);
            AnsiConsole.SetBold();
        }
        else if (line.StartsWith("[Result]"))
        {
            AnsiConsole.SetForeground(ConsoleColor.Cyan);
            AnsiConsole.SetBold();
            AnsiConsole.SetBackgroundRgb(0, 40, 50);
        }
        else if (line.StartsWith("[Input Needed]"))
        {
            AnsiConsole.SetForeground(ConsoleColor.Yellow);
            AnsiConsole.SetBold();
        }
        else if (line.StartsWith("[You]"))
            AnsiConsole.SetForeground(ConsoleColor.Cyan);
        else if (line.StartsWith("[Warn]"))
        {
            AnsiConsole.SetForeground(ConsoleColor.DarkYellow);
            AnsiConsole.SetBold();
        }
        else if (line.StartsWith("[Skill]"))
        {
            AnsiConsole.SetForeground(ConsoleColor.Blue);
            AnsiConsole.SetBold();
        }
        else if (line.StartsWith("[System]"))
            AnsiConsole.SetForeground(ConsoleColor.DarkCyan);
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
                                _scrollOffset = 0;
                            }
                            else
                            {
                                _displayLines.Add($"[System] {result}");
                                ScrollToEnd();
                            }
                            Draw();
                            AnsiConsole.Flush();
                        });
                    }
                });
            }
        }
        else if (_bot is not null && _bot.Status == BotStatus.WaitingForInput)
        {
            // Send input to bot
            var botEngine = _services.GetRequiredService<IBotEngine>();
            _displayLines.Add($"[You] {DateTime.Now:HH:mm:ss} {input}");
            ScrollToEnd();
            Draw();
            AnsiConsole.Flush();

            Task.Run(async () => await botEngine.SendInputAsync(_bot.Id, input));
        }
        else
        {
            _displayLines.Add($"[System] Bot is not waiting for input. Use / for commands.");
            ScrollToEnd();
            Draw();
            AnsiConsole.Flush();
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
        ScrollToEnd();
    }

    public void ClearOutput()
    {
        _displayLines.Clear();
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
