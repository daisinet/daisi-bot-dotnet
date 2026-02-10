using System.Text;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using DaisiBot.Tui.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Screens;

/// <summary>
/// Right panel showing chat messages, streaming responses, stats, and input line.
/// Three zones: message display (word-wrapped, scrollable), stats line, input line.
/// </summary>
public class ChatPanel
{
    private readonly App _app;
    private readonly IServiceProvider _services;
    private readonly IChatService _chatService;
    private Conversation? _conversation;
    private bool _isStreaming;

    // Input line editing
    private readonly StringBuilder _inputBuffer = new();
    private int _cursorPos;

    // Slash command autocomplete
    private readonly SlashCommandPopup _slashPopup = new();
    private int _lastPopupVisibleCount;

    // Message display
    private readonly List<string> _displayLines = [];
    private int _scrollOffset;

    // Stats
    private string _statsText = "";

    public int Top { get; set; }
    public int Left { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool HasFocus { get; set; }

    public SlashCommandDispatcher? CommandDispatcher { get; set; }

    // Height breakdown: title(1) + messages(H-4) + stats(1) + input(1) + bottom_border(1)
    private int MessageAreaTop => Top + 1;
    private int MessageAreaHeight => Height - 4;
    private int StatsRow => Top + Height - 3;
    private int InputRow => Top + Height - 2;

    public ChatPanel(App app, IServiceProvider services)
    {
        _app = app;
        _services = services;
        _chatService = services.GetRequiredService<IChatService>();
    }

    public void SetConversation(Conversation conversation)
    {
        _conversation = conversation;
        RebuildDisplayLines();
        _scrollOffset = Math.Max(0, _displayLines.Count - MessageAreaHeight);
        Draw();
    }

    public void ClearConversation()
    {
        _conversation = null;
        _displayLines.Clear();
        _statsText = "";
        _scrollOffset = 0;
        Draw();
    }

    public void Draw()
    {
        var contentWidth = Width - 2;

        // Title bar
        SetBorderColor();
        AnsiConsole.WriteAt(Top, Left, "\u250C");
        AnsiConsole.WriteAt(Top, Left + 1, new string('\u2500', Width - 2));
        AnsiConsole.WriteAt(Top, Left + Width - 1, "\u2510");
        ResetBorderColor();
        var title = _conversation is not null
            ? $" Chat: {Truncate(_conversation.Title, contentWidth - 8)} "
            : " Chat ";
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

        // Message area
        DrawMessages();

        // Stats line
        SetBorderColor();
        AnsiConsole.WriteAt(StatsRow, Left, "\u2502");
        ResetBorderColor();
        AnsiConsole.SetDim();
        AnsiConsole.WriteAt(StatsRow, Left + 1, Truncate(_statsText, contentWidth).PadRight(contentWidth));
        AnsiConsole.ResetStyle();
        SetBorderColor();
        AnsiConsole.WriteAt(StatsRow, Left + Width - 1, "\u2502");
        ResetBorderColor();

        // Input line
        DrawInputLine();

        // Slash command popup (above input line)
        _slashPopup.Draw(InputRow, Left, Width);

        // Bottom border
        var bottomRow = Top + Height - 1;
        SetBorderColor();
        AnsiConsole.WriteAt(bottomRow, Left, "\u2514");
        AnsiConsole.WriteAt(bottomRow, Left + 1, new string('\u2500', Width - 2));
        AnsiConsole.WriteAt(bottomRow, Left + Width - 1, "\u2518");
        ResetBorderColor();

        // Hints
        var hints = _isStreaming ? " Esc:Stop " : " Enter:Send ";
        if (hints.Length <= contentWidth)
        {
            var hintStart = Left + (Width - hints.Length) / 2;
            AnsiConsole.SetDim();
            AnsiConsole.WriteAt(bottomRow, hintStart, hints);
            AnsiConsole.ResetStyle();
        }

        // Position cursor at input
        if (HasFocus && !_isStreaming)
        {
            var cursorCol = Left + 3 + _cursorPos; // "│> " prefix
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

    private void DrawMessages()
    {
        var contentWidth = Width - 2;
        var areaHeight = MessageAreaHeight;

        // Ensure scroll offset is valid
        if (_scrollOffset > Math.Max(0, _displayLines.Count - areaHeight))
            _scrollOffset = Math.Max(0, _displayLines.Count - areaHeight);

        for (var i = 0; i < areaHeight; i++)
        {
            var row = MessageAreaTop + i;
            var lineIdx = _scrollOffset + i;

            SetBorderColor();
            AnsiConsole.WriteAt(row, Left, "\u2502");
            ResetBorderColor();

            if (lineIdx < _displayLines.Count)
            {
                var line = _displayLines[lineIdx];

                // Color coding based on prefix
                if (line.StartsWith("[You]"))
                {
                    AnsiConsole.SetForeground(ConsoleColor.Cyan);
                    AnsiConsole.WriteAt(row, Left + 1, Truncate(line, contentWidth).PadRight(contentWidth));
                    AnsiConsole.ResetStyle();
                }
                else if (line.StartsWith("[Bot]"))
                {
                    AnsiConsole.SetForeground(ConsoleColor.Green);
                    AnsiConsole.WriteAt(row, Left + 1, Truncate(line, contentWidth).PadRight(contentWidth));
                    AnsiConsole.ResetStyle();
                }
                else if (line.StartsWith("  [Thinking]"))
                {
                    AnsiConsole.SetDim();
                    AnsiConsole.SetForeground(ConsoleColor.DarkYellow);
                    AnsiConsole.WriteAt(row, Left + 1, Truncate(line, contentWidth).PadRight(contentWidth));
                    AnsiConsole.ResetStyle();
                }
                else
                {
                    AnsiConsole.WriteAt(row, Left + 1, Truncate(line, contentWidth).PadRight(contentWidth));
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
    }

    private void DrawInputLine()
    {
        var contentWidth = Width - 2;
        SetBorderColor();
        AnsiConsole.WriteAt(InputRow, Left, "\u2502");
        ResetBorderColor();

        var prompt = "> ";
        var inputMaxLen = contentWidth - prompt.Length;
        var displayInput = _inputBuffer.ToString();
        if (displayInput.Length > inputMaxLen)
            displayInput = displayInput[(displayInput.Length - inputMaxLen)..];

        AnsiConsole.SetForeground(ConsoleColor.Yellow);
        AnsiConsole.WriteAt(InputRow, Left + 1, prompt);
        AnsiConsole.ResetStyle();
        AnsiConsole.WriteAt(InputRow, Left + 1 + prompt.Length,
            displayInput.PadRight(contentWidth - prompt.Length));

        SetBorderColor();
        AnsiConsole.WriteAt(InputRow, Left + Width - 1, "\u2502");
        ResetBorderColor();

        // Reposition cursor
        if (HasFocus && !_isStreaming)
        {
            var cursorCol = Left + 3 + _cursorPos; // "│> " prefix
            if (cursorCol < Left + Width - 1)
            {
                AnsiConsole.MoveTo(InputRow, cursorCol);
                AnsiConsole.ShowCursor();
            }
        }
    }

    public bool HandleKey(ConsoleKeyInfo key)
    {
        if (!HasFocus) return false;

        // Streaming: only Esc is handled
        if (_isStreaming)
        {
            if (key.Key == ConsoleKey.Escape)
            {
                StopStreaming();
                return true;
            }
            return false;
        }

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
                    DrawMessages(); // repaint area the popup covered
                    DrawInputLine();
                    AnsiConsole.Flush();

                    if (!requiresArgs)
                        SendMessage();
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
                SendMessage();
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
                if (_cursorPos > 0)
                {
                    _cursorPos--;
                    DrawInputLine();
                }
                return true;

            case ConsoleKey.RightArrow:
                if (_cursorPos < _inputBuffer.Length)
                {
                    _cursorPos++;
                    DrawInputLine();
                }
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
                _scrollOffset = Math.Max(0, _scrollOffset - MessageAreaHeight);
                DrawMessages();
                return true;

            case ConsoleKey.PageDown:
                _scrollOffset = Math.Min(
                    Math.Max(0, _displayLines.Count - MessageAreaHeight),
                    _scrollOffset + MessageAreaHeight);
                DrawMessages();
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
            DrawMessages();
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
            DrawMessages();
        }
    }

    private void SendMessage()
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

        // Check for slash commands first
        if (SlashCommandParser.IsSlashCommand(input) && CommandDispatcher is not null)
        {
            _inputBuffer.Clear();
            _cursorPos = 0;
            var cmd = SlashCommandParser.Parse(input);
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
                            AddDisplayLine($"[System] {result}");
                            ScrollToEnd();
                        }
                        Draw();
                        AnsiConsole.Flush();
                    });
                }
            });
            DrawInputLine();
            return;
        }

        if (_conversation is null) return;

        _inputBuffer.Clear();
        _cursorPos = 0;
        _isStreaming = true;

        // Add user message to display
        var time = DateTime.Now.ToString("HH:mm");
        AddDisplayLine($"[You] {time}");
        AddWrappedLines($"  {input}", Width - 4);
        AddDisplayLine("");

        // Show streaming indicator
        AddDisplayLine($"[Bot] {time}");
        var streamLineStart = _displayLines.Count;
        AddDisplayLine("  ...");

        ScrollToEnd();
        Draw();
        AnsiConsole.Flush();

        // Fire async send
        Task.Run(async () =>
        {
            try
            {
                var settingsService = _services.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetSettingsAsync();

                var config = new AgentConfig
                {
                    ModelName = !string.IsNullOrWhiteSpace(_conversation.ModelName)
                        ? _conversation.ModelName
                        : settings.DefaultModelName,
                    InitializationPrompt = _conversation.SystemPrompt,
                    ThinkLevel = _conversation.ThinkLevel,
                    Temperature = settings.Temperature,
                    TopP = settings.TopP,
                    MaxTokens = settings.MaxTokens,
                    EnabledToolGroups = settings.GetEnabledToolGroups(),
                    UseHostMode = settings.HostModeEnabled
                };

                var streamBuf = new StringBuilder();

                await foreach (var chunk in _chatService.SendMessageAsync(
                    _conversation.Id, input, config))
                {
                    if (chunk.IsComplete) break;

                    streamBuf.Append(chunk.Content);

                    var captured = streamBuf.ToString();
                    _app.Post(() =>
                    {
                        // Replace streaming lines
                        while (_displayLines.Count > streamLineStart)
                            _displayLines.RemoveAt(_displayLines.Count - 1);

                        var wrapped = WordWrap($"  {captured}", Width - 4);
                        _displayLines.AddRange(wrapped);
                        ScrollToEnd();
                        DrawMessages();
                        AnsiConsole.Flush();
                    });
                }

                // Get stats
                var stats = await _chatService.GetCurrentStatsAsync();

                _app.Post(() =>
                {
                    _statsText = $"Tokens: {stats.LastMessageTokenCount} | " +
                                 $"{stats.TokensPerSecond:F1} t/s | " +
                                 $"{stats.LastMessageComputeTimeMs:F0}ms | " +
                                 $"Session: {stats.SessionTokenCount}";
                    AddDisplayLine("");
                    _isStreaming = false;
                    ScrollToEnd();
                    Draw();
                    AnsiConsole.Flush();
                });

                // Reload conversation to get persisted messages
                var store = _services.GetRequiredService<IConversationStore>();
                var updated = await store.GetAsync(_conversation.Id);
                if (updated is not null)
                    _app.Post(() => _conversation = updated);
            }
            catch (OperationCanceledException)
            {
                _app.Post(() =>
                {
                    AddDisplayLine("  [Stopped]");
                    AddDisplayLine("");
                    _isStreaming = false;
                    ScrollToEnd();
                    Draw();
                    AnsiConsole.Flush();
                });
            }
            catch (Exception ex)
            {
                _app.Post(() =>
                {
                    AddDisplayLine($"  [Error: {ex.Message}]");
                    AddDisplayLine("");
                    _isStreaming = false;
                    ScrollToEnd();
                    Draw();
                    AnsiConsole.Flush();
                });
            }
        });
    }

    private void StopStreaming()
    {
        Task.Run(async () =>
        {
            await _chatService.StopGenerationAsync();
        });
    }

    private void RebuildDisplayLines()
    {
        _displayLines.Clear();
        if (_conversation is null) return;

        foreach (var msg in _conversation.Messages)
        {
            var rolePrefix = msg.Role switch
            {
                ChatRole.User => "[You]",
                ChatRole.Assistant => "[Bot]",
                _ => $"[{msg.Role}]"
            };

            var time = msg.Timestamp.ToLocalTime().ToString("HH:mm");
            var statsInfo = msg.Role == ChatRole.Assistant && msg.TokenCount > 0
                ? $" ({msg.TokenCount} tok, {msg.TokensPerSecond:F1} t/s)"
                : "";

            AddDisplayLine($"{rolePrefix} {time}{statsInfo}");

            var prefix = msg.Type == ChatMessageType.Thinking ? "  [Thinking] " : "  ";
            AddWrappedLines($"{prefix}{msg.Content}", Width - 4);
            AddDisplayLine("");
        }
    }

    private void AddDisplayLine(string line) => _displayLines.Add(line);

    private void AddWrappedLines(string text, int maxWidth)
    {
        if (maxWidth <= 0) maxWidth = 40;
        var lines = WordWrap(text, maxWidth);
        _displayLines.AddRange(lines);
    }

    private static List<string> WordWrap(string text, int maxWidth)
    {
        if (maxWidth <= 0) maxWidth = 40;
        var result = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            result.Add("");
            return result;
        }

        // Split on newlines first, then wrap each line
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            var clean = line.TrimEnd('\r');
            if (clean.Length == 0)
            {
                result.Add("");
                continue;
            }

            var remaining = clean;
            while (remaining.Length > maxWidth)
            {
                var breakPos = remaining.LastIndexOf(' ', maxWidth);
                if (breakPos <= 0) breakPos = maxWidth;

                result.Add(remaining[..breakPos]);
                remaining = remaining[breakPos..].TrimStart();
            }
            if (remaining.Length > 0)
                result.Add(remaining);
        }

        return result;
    }

    private void ScrollToEnd()
    {
        _scrollOffset = Math.Max(0, _displayLines.Count - MessageAreaHeight);
    }

    private void SetBorderColor()
    {
        if (HasFocus) AnsiConsole.SetForeground(ConsoleColor.Green);
    }

    private void ResetBorderColor()
    {
        if (HasFocus) AnsiConsole.ResetStyle();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 2)] + "..";
}
