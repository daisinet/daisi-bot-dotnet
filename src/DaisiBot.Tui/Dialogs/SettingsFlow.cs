using System.Text;
using Daisi.SDK.Models;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using DaisiBot.Core.Security;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Dialogs;

/// <summary>
/// Multi-page settings editor. Pages: General, Inference, Connection, System Prompt, Tools.
/// Left/Right arrows switch pages, Up/Down navigate fields, Enter edits.
/// </summary>
public class SettingsFlow : IModal
{
    private readonly App _app;
    private readonly ISettingsService _settingsService;
    private readonly IAuthService _authService;
    private DialogRunner.BoxBounds? _box;
    private UserSettings _settings = new();
    private bool _loaded;
    private string _originalOrcDomain = "";
    private int _originalOrcPort;

    private enum Page { General, Inference, Connection, Prompt, Tools }
    private Page _currentPage = Page.General;
    private int _fieldIndex;

    // Inference page fields
    private readonly StringBuilder _tempField = new("0.7");
    private readonly StringBuilder _topPField = new("0.9");
    private readonly StringBuilder _maxTokensField = new("32000");

    // Connection page fields
    private readonly StringBuilder _orcDomainField = new("orc.daisinet.com");
    private readonly StringBuilder _orcPortField = new("443");
    private bool _orcUseSsl = true;

    // System Prompt
    private readonly StringBuilder _systemPromptField = new();

    // General
    private bool _fileLoggingEnabled;
    private bool _logInferenceOutput;

    // Tools
    private readonly bool[] _toolChecks;
    private readonly ToolGroupSelection[] _allGroups;

    private bool _editing;
    private int _editCursor;
    private bool _saving;
    private string _statusText = "";
    private ConsoleColor _statusColor = ConsoleColor.Gray;

    private static readonly string[] PageNames = ["General", "Inference", "Connection", "Prompt", "Tools"];

    public SettingsFlow(App app, IServiceProvider services)
    {
        _app = app;
        _settingsService = services.GetRequiredService<ISettingsService>();
        _authService = services.GetRequiredService<IAuthService>();
        _allGroups = Enum.GetValues<ToolGroupSelection>();
        _toolChecks = new bool[_allGroups.Length];
        LoadSettings();
    }

    private void LoadSettings()
    {
        Task.Run(async () =>
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                _app.Post(() =>
                {
                    _settings = settings;
                    _tempField.Clear().Append(settings.Temperature.ToString("F1"));
                    _topPField.Clear().Append(settings.TopP.ToString("F1"));
                    _maxTokensField.Clear().Append(settings.MaxTokens.ToString());
                    _orcDomainField.Clear().Append(settings.OrcDomain);
                    _orcPortField.Clear().Append(settings.OrcPort.ToString());
                    _orcUseSsl = settings.OrcUseSsl;
                    _originalOrcDomain = settings.OrcDomain;
                    _originalOrcPort = settings.OrcPort;
                    _systemPromptField.Clear().Append(settings.SystemPrompt);

                    var enabled = settings.GetEnabledToolGroups();
                    for (var i = 0; i < _allGroups.Length; i++)
                        _toolChecks[i] = enabled.Contains(_allGroups[i]);
                    _fileLoggingEnabled = settings.BotFileLoggingEnabled;
                    _logInferenceOutput = settings.LogInferenceOutputEnabled;

                    _loaded = true;
                    Draw();
                    AnsiConsole.Flush();
                });
            }
            catch
            {
                _app.Post(() =>
                {
                    _loaded = true;
                    Draw();
                    AnsiConsole.Flush();
                });
            }
        });
    }

    public void Draw()
    {
        _box = DialogRunner.DrawCenteredBox(_app, "Settings", 60, 22);

        // Page tabs
        var tabRow = _box.InnerTop;
        var tabCol = _box.InnerLeft;
        for (var i = 0; i < PageNames.Length; i++)
        {
            var label = $" {PageNames[i]} ";
            if ((int)_currentPage == i)
            {
                AnsiConsole.SetReverse();
                AnsiConsole.WriteAt(tabRow, tabCol, label);
                AnsiConsole.ResetStyle();
            }
            else
            {
                AnsiConsole.SetDim();
                AnsiConsole.WriteAt(tabRow, tabCol, label);
                AnsiConsole.ResetStyle();
            }
            tabCol += label.Length + 1;
        }

        // Clear content area
        var contentTop = _box.InnerTop + 2;
        var contentHeight = _box.InnerHeight - 4;
        AnsiConsole.ClearRegion(contentTop, _box.InnerLeft, contentHeight, _box.InnerWidth);

        // Draw current page
        switch (_currentPage)
        {
            case Page.General:
                DrawGeneralPage(contentTop);
                break;
            case Page.Inference:
                DrawInferencePage(contentTop);
                break;
            case Page.Connection:
                DrawConnectionPage(contentTop);
                break;
            case Page.Prompt:
                DrawPromptPage(contentTop);
                break;
            case Page.Tools:
                DrawToolsPage(contentTop);
                break;
        }

        if (_statusText.Length > 0)
            DialogRunner.DrawStatus(_box, _statusText, _statusColor);

        DialogRunner.DrawButtonHints(_box, " \u2190\u2192:Page  \u2191\u2193:Field  Enter:Edit  S:Save  Esc:Cancel ");
    }

    private void DrawGeneralPage(int top)
    {
        AnsiConsole.WriteAt(top, _box!.InnerLeft, "Logging:");

        DrawCheckboxRow(top + 1, 0, "Log bot runs to file (daisi-bot-logs/)", _fileLoggingEnabled);
        DrawCheckboxRow(top + 2, 1, "Log inference output", _logInferenceOutput);
    }

    private void DrawCheckboxRow(int row, int fieldIdx, string label, bool value)
    {
        var check = value ? "[x]" : "[ ]";
        var text = $" {check} {label}";

        if (_fieldIndex == fieldIdx)
        {
            AnsiConsole.SetReverse();
            AnsiConsole.WriteAt(row, _box!.InnerLeft, text.PadRight(_box.InnerWidth));
            AnsiConsole.ResetStyle();
        }
        else
        {
            AnsiConsole.WriteAt(row, _box!.InnerLeft, text.PadRight(_box.InnerWidth));
        }
    }

    private void DrawInferencePage(int top)
    {
        DrawFieldRow(top, 0, "Temperature:", _tempField.ToString(), _fieldIndex == 0);
        DrawFieldRow(top, 2, "Top P:", _topPField.ToString(), _fieldIndex == 1);
        DrawFieldRow(top, 4, "Max Tokens:", _maxTokensField.ToString(), _fieldIndex == 2);
    }

    private void DrawConnectionPage(int top)
    {
        DrawFieldRow(top, 0, "Orc Domain:", _orcDomainField.ToString(), _fieldIndex == 0);
        DrawFieldRow(top, 2, "Orc Port:", _orcPortField.ToString(), _fieldIndex == 1);

        var sslText = _orcUseSsl ? "[x] Use SSL" : "[ ] Use SSL";
        var sslFocused = _fieldIndex == 2;
        if (sslFocused)
        {
            AnsiConsole.SetReverse();
            AnsiConsole.WriteAt(top + 4, _box!.InnerLeft, sslText.PadRight(_box.InnerWidth));
            AnsiConsole.ResetStyle();
        }
        else
        {
            AnsiConsole.WriteAt(top + 4, _box!.InnerLeft, sslText);
        }
    }

    private void DrawPromptPage(int top)
    {
        AnsiConsole.WriteAt(top, _box!.InnerLeft, "System Prompt:");
        var lines = WordWrapSimple(_systemPromptField.ToString(), _box.InnerWidth);
        for (var i = 0; i < Math.Min(lines.Count, 10); i++)
        {
            if (_editing)
                AnsiConsole.SetReverse();
            AnsiConsole.WriteAt(top + 1 + i, _box.InnerLeft, lines[i].PadRight(_box.InnerWidth));
            if (_editing)
                AnsiConsole.ResetStyle();
        }

        if (_editing)
        {
            AnsiConsole.ShowCursor();
            // Position cursor at end
            var cursorRow = top + 1 + Math.Min(lines.Count - 1, 9);
            var cursorCol = _box.InnerLeft + (lines.Count > 0 ? lines[^1].Length : 0);
            AnsiConsole.MoveTo(cursorRow, cursorCol);
        }
    }

    private void DrawToolsPage(int top)
    {
        AnsiConsole.WriteAt(top, _box!.InnerLeft, "Enable Tool Groups:");
        for (var i = 0; i < _allGroups.Length; i++)
        {
            var group = _allGroups[i];
            var level = ToolPermissions.GetPermissionLevel(group);
            var elevated = level == ToolPermissionLevel.Elevated ? " [ELEVATED]" : "";
            var check = _toolChecks[i] ? "[x]" : "[ ]";
            var text = $" {check} {group}{elevated}";
            var row = top + 1 + i;

            if (i == _fieldIndex)
            {
                AnsiConsole.SetReverse();
                AnsiConsole.WriteAt(row, _box.InnerLeft, text.PadRight(_box.InnerWidth));
                AnsiConsole.ResetStyle();
            }
            else
            {
                if (level == ToolPermissionLevel.Elevated)
                    AnsiConsole.SetForeground(ConsoleColor.Yellow);
                AnsiConsole.WriteAt(row, _box.InnerLeft, text.PadRight(_box.InnerWidth));
                AnsiConsole.ResetStyle();
            }
        }

    }

    private void DrawFieldRow(int top, int rowOffset, string label, string value, bool focused)
    {
        AnsiConsole.WriteAt(top + rowOffset, _box!.InnerLeft, label);
        var valueCol = _box.InnerLeft;
        var valueRow = top + rowOffset + 1;
        var display = value.PadRight(_box.InnerWidth);
        if (display.Length > _box.InnerWidth) display = display[.._box.InnerWidth];

        if (focused && _editing)
        {
            AnsiConsole.SetReverse();
            AnsiConsole.WriteAt(valueRow, valueCol, display);
            AnsiConsole.ResetStyle();
            AnsiConsole.ShowCursor();
            AnsiConsole.MoveTo(valueRow, valueCol + _editCursor);
        }
        else if (focused)
        {
            AnsiConsole.SetForeground(ConsoleColor.Cyan);
            AnsiConsole.WriteAt(valueRow, valueCol, display);
            AnsiConsole.ResetStyle();
        }
        else
        {
            AnsiConsole.WriteAt(valueRow, valueCol, display);
        }
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        // Always allow Escape, even while loading
        if (key.Key == ConsoleKey.Escape && !_editing)
        {
            _app.CloseModal();
            return;
        }

        if (!_loaded || _saving) return;

        if (_editing)
        {
            HandleEditKey(key);
            return;
        }

        switch (key.Key)
        {
            case ConsoleKey.Escape:
                _app.CloseModal();
                break;

            case ConsoleKey.LeftArrow:
                if (_currentPage > Page.General)
                {
                    _currentPage--;
                    _fieldIndex = 0;
                    _editing = false;
                }
                Draw();
                break;

            case ConsoleKey.RightArrow:
                if (_currentPage < Page.Tools)
                {
                    _currentPage++;
                    _fieldIndex = 0;
                    _editing = false;
                }
                Draw();
                break;

            case ConsoleKey.UpArrow:
                if (_fieldIndex > 0) _fieldIndex--;
                Draw();
                break;

            case ConsoleKey.DownArrow:
                _fieldIndex++;
                ClampFieldIndex();
                Draw();
                break;

            case ConsoleKey.Enter:
            case ConsoleKey.Spacebar:
                OnActivateField();
                Draw();
                break;

            case ConsoleKey.S:
                Save();
                break;
        }
    }

    private void HandleEditKey(ConsoleKeyInfo key)
    {
        var buf = GetCurrentEditBuffer();
        if (buf is null) return;

        switch (key.Key)
        {
            case ConsoleKey.Escape:
            case ConsoleKey.Enter when _currentPage != Page.Prompt:
                _editing = false;
                AnsiConsole.HideCursor();
                Draw();
                break;

            case ConsoleKey.Backspace:
                if (_editCursor > 0)
                {
                    buf.Remove(_editCursor - 1, 1);
                    _editCursor--;
                }
                Draw();
                break;

            case ConsoleKey.Delete:
                if (_editCursor < buf.Length)
                    buf.Remove(_editCursor, 1);
                Draw();
                break;

            case ConsoleKey.LeftArrow:
                if (_editCursor > 0) _editCursor--;
                Draw();
                break;

            case ConsoleKey.RightArrow:
                if (_editCursor < buf.Length) _editCursor++;
                Draw();
                break;

            case ConsoleKey.Home:
                _editCursor = 0;
                Draw();
                break;

            case ConsoleKey.End:
                _editCursor = buf.Length;
                Draw();
                break;

            case ConsoleKey.Enter when _currentPage == Page.Prompt:
                buf.Insert(_editCursor, '\n');
                _editCursor++;
                Draw();
                break;

            default:
                if (key.KeyChar >= 32 && key.KeyChar < 127)
                {
                    buf.Insert(_editCursor, key.KeyChar);
                    _editCursor++;
                    Draw();
                }
                break;
        }
    }

    private void OnActivateField()
    {
        switch (_currentPage)
        {
            case Page.General:
                if (_fieldIndex == 0) _fileLoggingEnabled = !_fileLoggingEnabled;
                if (_fieldIndex == 1) _logInferenceOutput = !_logInferenceOutput;
                break;

            case Page.Inference:
            case Page.Connection when _fieldIndex < 2:
            case Page.Prompt:
                _editing = true;
                var buf = GetCurrentEditBuffer();
                _editCursor = buf?.Length ?? 0;
                break;

            case Page.Connection when _fieldIndex == 2:
                _orcUseSsl = !_orcUseSsl;
                break;

            case Page.Tools:
                if (_fieldIndex < _toolChecks.Length)
                {
                    var group = _allGroups[_fieldIndex];
                    if (!_toolChecks[_fieldIndex] && ToolPermissions.IsElevated(group))
                    {
                        // Show confirmation
                        var desc = ToolPermissions.GetDescription(group);
                        var confirm = new ConfirmDialog(_app,
                            $"'{group}' requires elevated access. Allow?",
                            allowed =>
                            {
                                if (allowed) _toolChecks[_fieldIndex] = true;
                                Draw();
                                AnsiConsole.Flush();
                            });
                        _app.RunModal(confirm);
                        return;
                    }
                    _toolChecks[_fieldIndex] = !_toolChecks[_fieldIndex];
                }
                break;
        }
    }

    private StringBuilder? GetCurrentEditBuffer()
    {
        return _currentPage switch
        {
            Page.Inference => _fieldIndex switch
            {
                0 => _tempField,
                1 => _topPField,
                2 => _maxTokensField,
                _ => null
            },
            Page.Connection => _fieldIndex switch
            {
                0 => _orcDomainField,
                1 => _orcPortField,
                _ => null
            },
            Page.Prompt => _systemPromptField,
            _ => null
        };
    }

    private void ClampFieldIndex()
    {
        var max = _currentPage switch
        {
            Page.General => 1,
            Page.Inference => 2,
            Page.Connection => 2,
            Page.Prompt => 0,
            Page.Tools => _allGroups.Length - 1,
            _ => 0
        };
        if (_fieldIndex > max) _fieldIndex = max;
    }

    private void Save()
    {
        if (float.TryParse(_tempField.ToString(), out var temp))
            _settings.Temperature = temp;
        if (float.TryParse(_topPField.ToString(), out var topP))
            _settings.TopP = topP;
        if (int.TryParse(_maxTokensField.ToString(), out var maxTokens))
            _settings.MaxTokens = maxTokens;

        _settings.OrcDomain = _orcDomainField.ToString();
        if (int.TryParse(_orcPortField.ToString(), out var port))
            _settings.OrcPort = port;
        _settings.OrcUseSsl = _orcUseSsl;
        _settings.SystemPrompt = _systemPromptField.ToString();

        var enabledGroups = new List<ToolGroupSelection>();
        for (var i = 0; i < _allGroups.Length; i++)
        {
            if (_toolChecks[i])
                enabledGroups.Add(_allGroups[i]);
        }
        _settings.SetEnabledToolGroups(enabledGroups);
        _settings.BotFileLoggingEnabled = _fileLoggingEnabled;
        _settings.LogInferenceOutputEnabled = _logInferenceOutput;

        _saving = true;
        _statusText = "Saving...";
        _statusColor = ConsoleColor.Yellow;
        Draw();
        AnsiConsole.Flush();

        Task.Run(async () =>
        {
            try
            {
                await _settingsService.SaveSettingsAsync(_settings);
                DaisiStaticSettings.ApplyUserSettings(
                    _settings.OrcDomain, _settings.OrcPort, _settings.OrcUseSsl);

                // If the Orc changed, the old client key is invalid for the new Orc
                var orcChanged = _settings.OrcDomain != _originalOrcDomain
                              || _settings.OrcPort != _originalOrcPort;
                if (orcChanged)
                    await _authService.LogoutAsync();

                _app.Post(() => _app.CloseModal());
            }
            catch (Exception ex)
            {
                _app.Post(() =>
                {
                    _saving = false;
                    _statusText = $"Save failed: {ex.Message}";
                    _statusColor = ConsoleColor.Red;
                    Draw();
                    AnsiConsole.Flush();
                });
            }
        });
    }

    private static List<string> WordWrapSimple(string text, int maxWidth)
    {
        if (maxWidth <= 0) maxWidth = 40;
        var result = new List<string>();
        if (string.IsNullOrEmpty(text)) { result.Add(""); return result; }

        foreach (var line in text.Split('\n'))
        {
            var remaining = line;
            while (remaining.Length > maxWidth)
            {
                var breakPos = remaining.LastIndexOf(' ', maxWidth);
                if (breakPos <= 0) breakPos = maxWidth;
                result.Add(remaining[..breakPos]);
                remaining = remaining[breakPos..].TrimStart();
            }
            result.Add(remaining);
        }
        return result;
    }
}
