using System.Text;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using DaisiBot.Core.Models.Skills;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Dialogs;

public class BotUpdateFlow : IModal
{
    private readonly App _app;
    private readonly IBotStore _botStore;
    private readonly IBotEngine _botEngine;
    private readonly BotInstance _bot;
    private readonly Action _onUpdated;
    private DialogRunner.BoxBounds? _box;

    private enum Field { Label, Goal, Persona, Schedule, IntervalInput, Skills, Steps }
    private Field _field = Field.Label;

    private readonly StringBuilder _labelBuffer = new();
    private readonly StringBuilder _goalBuffer = new();
    private readonly StringBuilder _personaBuffer = new();
    private readonly StringBuilder _intervalBuffer = new();
    private int _labelCursor;
    private int _goalCursor;
    private int _personaCursor;
    private int _intervalCursor;
    private int _scheduleIndex;

    // Skills
    private List<(string Id, string Name, bool Enabled)> _skillItems = [];
    private int _skillSelectedIndex;
    private int _skillScrollOffset;

    // Steps
    private readonly List<string> _steps = [];
    private int _stepSelectedIndex;
    private bool _addingStep;
    private readonly StringBuilder _newStepBuffer = new();
    private int _newStepCursor;

    private string _statusText = "";
    private ConsoleColor _statusColor = ConsoleColor.Gray;
    private bool _busy;

    private static readonly (string Label, BotScheduleType Type)[] ScheduleOptions =
    [
        ("Run Once", BotScheduleType.Once),
        ("Interval (custom minutes)", BotScheduleType.Interval),
        ("Hourly", BotScheduleType.Hourly),
        ("Daily", BotScheduleType.Daily),
        ("Continuous", BotScheduleType.Continuous),
    ];

    public BotUpdateFlow(App app, IServiceProvider services, BotInstance bot, Action onUpdated)
    {
        _app = app;
        _botStore = services.GetRequiredService<IBotStore>();
        _botEngine = services.GetRequiredService<IBotEngine>();
        _bot = bot;
        _onUpdated = onUpdated;

        // Pre-populate from existing bot
        _labelBuffer.Append(bot.Label);
        _labelCursor = _labelBuffer.Length;
        _goalBuffer.Append(bot.Goal);
        _goalCursor = _goalBuffer.Length;
        if (!string.IsNullOrWhiteSpace(bot.Persona))
        {
            _personaBuffer.Append(bot.Persona);
            _personaCursor = _personaBuffer.Length;
        }
        _scheduleIndex = Array.FindIndex(ScheduleOptions, o => o.Type == bot.ScheduleType);
        if (_scheduleIndex < 0) _scheduleIndex = 0;
        if (bot.ScheduleType == BotScheduleType.Interval)
        {
            _intervalBuffer.Append(bot.ScheduleIntervalMinutes.ToString());
            _intervalCursor = _intervalBuffer.Length;
        }

        var enabledSkillIds = new HashSet<string>(bot.GetEnabledSkillIds(), StringComparer.OrdinalIgnoreCase);

        // Load steps and skills async
        var skillFileLoader = services.GetRequiredService<ISkillFileLoader>();
        Task.Run(async () =>
        {
            var steps = await _botStore.GetStepsAsync(bot.Id);
            var allSkills = await skillFileLoader.LoadAllAsync();

            _app.Post(() =>
            {
                foreach (var s in steps)
                    _steps.Add(s.Description);

                _skillItems = allSkills
                    .OrderBy(s => s.Name)
                    .Select(s => (s.Id, s.Name, enabledSkillIds.Contains(s.Id)))
                    .ToList();

                Draw();
                AnsiConsole.Flush();
            });
        });
    }

    public void Draw()
    {
        var skillRowCount = Math.Max(_skillItems.Count, 1);
        var boxHeight = 30 + Math.Min(skillRowCount, 6) + Math.Max(_steps.Count, 1);
        if (boxHeight > _app.Height - 4) boxHeight = _app.Height - 4;
        _box = DialogRunner.DrawCenteredBox(_app, "Edit Bot", 60, boxHeight);

        var row = 0;

        // Label
        DialogRunner.DrawLabel(_box, row, "Label:");
        row++;
        DialogRunner.DrawTextField(_box, row, _labelBuffer.ToString(), _field == Field.Label && !_busy);
        row += 2;

        // Goal
        DialogRunner.DrawLabel(_box, row, "Goal:");
        row++;
        DialogRunner.DrawTextField(_box, row, _goalBuffer.ToString(), _field == Field.Goal && !_busy);
        row += 2;

        // Persona
        DialogRunner.DrawLabel(_box, row, "Persona (optional):");
        row++;
        DialogRunner.DrawTextField(_box, row, _personaBuffer.ToString(), _field == Field.Persona && !_busy);
        row += 2;

        // Schedule
        DialogRunner.DrawLabel(_box, row, "Schedule:");
        row++;
        for (var i = 0; i < ScheduleOptions.Length; i++)
        {
            if (row >= _box.InnerHeight - 3) break;
            var prefix = i == _scheduleIndex ? "> " : "  ";
            var text = $"{prefix}{ScheduleOptions[i].Label}";
            if (i == _scheduleIndex && _field == Field.Schedule)
            {
                AnsiConsole.SetReverse();
                AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, text.PadRight(_box.InnerWidth));
                AnsiConsole.ResetStyle();
            }
            else
            {
                AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, text.PadRight(_box.InnerWidth));
            }
            row++;
        }

        // Interval input
        if (ScheduleOptions[_scheduleIndex].Type == BotScheduleType.Interval)
        {
            DialogRunner.DrawLabel(_box, row, "Minutes between runs:");
            row++;
            DialogRunner.DrawTextField(_box, row, _intervalBuffer.ToString(), _field == Field.IntervalInput && !_busy);
            row++;
        }
        row++;

        // Skills
        DialogRunner.DrawLabel(_box, row, "Skills (Space to toggle):");
        row++;

        if (_skillItems.Count == 0)
        {
            AnsiConsole.SetDim();
            AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, "  (loading...)".PadRight(_box.InnerWidth));
            AnsiConsole.ResetStyle();
            row++;
        }
        else
        {
            var maxSkillRows = Math.Min(_skillItems.Count, 6);
            // Adjust scroll offset to keep selected item visible
            if (_skillSelectedIndex < _skillScrollOffset)
                _skillScrollOffset = _skillSelectedIndex;
            if (_skillSelectedIndex >= _skillScrollOffset + maxSkillRows)
                _skillScrollOffset = _skillSelectedIndex - maxSkillRows + 1;

            for (var vi = 0; vi < maxSkillRows; vi++)
            {
                if (row >= _box.InnerHeight - 3) break;
                var i = _skillScrollOffset + vi;
                if (i >= _skillItems.Count) break;

                var (id, name, enabled) = _skillItems[i];
                var check = enabled ? "[x]" : "[ ]";
                var pointer = (_field == Field.Skills && i == _skillSelectedIndex) ? "> " : "  ";
                var skillText = $"{pointer}{check} {name}";
                if (skillText.Length > _box.InnerWidth)
                    skillText = skillText[..(_box.InnerWidth - 2)] + "..";

                if (_field == Field.Skills && i == _skillSelectedIndex)
                {
                    AnsiConsole.SetReverse();
                    AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, skillText.PadRight(_box.InnerWidth));
                    AnsiConsole.ResetStyle();
                }
                else
                {
                    AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, skillText.PadRight(_box.InnerWidth));
                }
                row++;
            }

            // Scroll indicator
            if (_skillItems.Count > maxSkillRows && row < _box.InnerHeight - 3)
            {
                var indicator = $"  ({_skillItems.Count(_s => _s.Enabled)} of {_skillItems.Count} enabled)";
                AnsiConsole.SetDim();
                AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, indicator.PadRight(_box.InnerWidth));
                AnsiConsole.ResetStyle();
                row++;
            }
        }
        row++;

        // Steps
        DialogRunner.DrawLabel(_box, row, "Steps (optional, overrides AI planning):");
        row++;

        if (_steps.Count == 0 && !_addingStep)
        {
            AnsiConsole.SetDim();
            AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, "  (no custom steps)".PadRight(_box.InnerWidth));
            AnsiConsole.ResetStyle();
            row++;
        }
        else
        {
            for (var i = 0; i < _steps.Count; i++)
            {
                if (row >= _box.InnerHeight - 3) break;
                var prefix = (_field == Field.Steps && i == _stepSelectedIndex && !_addingStep) ? "> " : "  ";
                var stepText = $"{prefix}{i + 1}. {_steps[i]}";
                if (stepText.Length > _box.InnerWidth)
                    stepText = stepText[..(_box.InnerWidth - 2)] + "..";

                if (_field == Field.Steps && i == _stepSelectedIndex && !_addingStep)
                {
                    AnsiConsole.SetReverse();
                    AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, stepText.PadRight(_box.InnerWidth));
                    AnsiConsole.ResetStyle();
                }
                else
                {
                    AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, stepText.PadRight(_box.InnerWidth));
                }
                row++;
            }
        }

        // New step input line
        if (_addingStep)
        {
            if (row < _box.InnerHeight - 3)
            {
                DialogRunner.DrawTextField(_box, row, _newStepBuffer.ToString(), true);
                row++;
            }
        }

        // Clear remaining rows
        while (row < _box.InnerHeight - 2)
        {
            AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, new string(' ', _box.InnerWidth));
            row++;
        }

        if (_statusText.Length > 0)
            DialogRunner.DrawStatus(_box, _statusText, _statusColor);

        var hints = _field switch
        {
            Field.Skills => " Tab:Fields  Space:Toggle  Enter:Save  Esc:Cancel ",
            Field.Steps => " Tab:Fields  A:Add  D:Del  Enter:Save  Esc:Cancel ",
            _ => " Tab:Next  Enter:Save  Esc:Cancel "
        };
        DialogRunner.DrawButtonHints(_box, hints);

        // Cursor
        if (!_busy && _box is not null)
        {
            AnsiConsole.ShowCursor();
            switch (_field)
            {
                case Field.Label:
                    AnsiConsole.MoveTo(_box.InnerTop + 1, _box.InnerLeft + _labelCursor);
                    break;
                case Field.Goal:
                    AnsiConsole.MoveTo(_box.InnerTop + 4, _box.InnerLeft + _goalCursor);
                    break;
                case Field.Persona:
                    AnsiConsole.MoveTo(_box.InnerTop + 7, _box.InnerLeft + _personaCursor);
                    break;
                case Field.Schedule:
                case Field.Skills:
                case Field.Steps:
                    AnsiConsole.HideCursor();
                    break;
            }
        }
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        if (_busy) return;

        if (key.Key == ConsoleKey.Escape)
        {
            if (_addingStep)
            {
                _addingStep = false;
                _newStepBuffer.Clear();
                _newStepCursor = 0;
                Draw();
                AnsiConsole.Flush();
                return;
            }
            AnsiConsole.HideCursor();
            _app.CloseModal();
            return;
        }

        if (_addingStep)
        {
            HandleNewStepInput(key);
            return;
        }

        // Tab cycles through fields
        if (key.Key == ConsoleKey.Tab)
        {
            _field = _field switch
            {
                Field.Label => Field.Goal,
                Field.Goal => Field.Persona,
                Field.Persona => Field.Schedule,
                Field.Schedule when ScheduleOptions[_scheduleIndex].Type == BotScheduleType.Interval => Field.IntervalInput,
                Field.Schedule => Field.Skills,
                Field.IntervalInput => Field.Skills,
                Field.Skills => Field.Steps,
                Field.Steps => Field.Label,
                _ => Field.Label
            };
            Draw();
            AnsiConsole.Flush();
            return;
        }

        // Enter to save (from non-list fields)
        if (key.Key == ConsoleKey.Enter && _field != Field.Schedule && _field != Field.Skills && _field != Field.Steps)
        {
            SaveBot();
            return;
        }

        switch (_field)
        {
            case Field.Label: HandleTextInput(key, _labelBuffer, ref _labelCursor); break;
            case Field.Goal: HandleTextInput(key, _goalBuffer, ref _goalCursor); break;
            case Field.Persona: HandleTextInput(key, _personaBuffer, ref _personaCursor); break;
            case Field.Schedule: HandleScheduleInput(key); break;
            case Field.IntervalInput: HandleIntervalInput(key); break;
            case Field.Skills: HandleSkillsInput(key); break;
            case Field.Steps: HandleStepsInput(key); break;
        }
    }

    private void HandleScheduleInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (_scheduleIndex > 0) { _scheduleIndex--; Draw(); AnsiConsole.Flush(); }
                break;
            case ConsoleKey.DownArrow:
                if (_scheduleIndex < ScheduleOptions.Length - 1) { _scheduleIndex++; Draw(); AnsiConsole.Flush(); }
                break;
            case ConsoleKey.Enter:
                // Move to next field
                if (ScheduleOptions[_scheduleIndex].Type == BotScheduleType.Interval)
                {
                    _field = Field.IntervalInput;
                    if (_intervalBuffer.Length == 0)
                    {
                        _intervalBuffer.Append("5");
                        _intervalCursor = _intervalBuffer.Length;
                    }
                }
                else
                {
                    _field = Field.Skills;
                }
                Draw();
                AnsiConsole.Flush();
                break;
        }
    }

    private void HandleIntervalInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Enter)
        {
            _field = Field.Skills;
            Draw();
            AnsiConsole.Flush();
            return;
        }

        if (key.KeyChar >= '0' && key.KeyChar <= '9')
        {
            _intervalBuffer.Insert(_intervalCursor, key.KeyChar);
            _intervalCursor++;
            Draw();
            AnsiConsole.Flush();
        }
        else
        {
            HandleTextInput(key, _intervalBuffer, ref _intervalCursor);
        }
    }

    private void HandleSkillsInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (_skillSelectedIndex > 0) { _skillSelectedIndex--; Draw(); AnsiConsole.Flush(); }
                break;
            case ConsoleKey.DownArrow:
                if (_skillSelectedIndex < _skillItems.Count - 1) { _skillSelectedIndex++; Draw(); AnsiConsole.Flush(); }
                break;
            case ConsoleKey.Spacebar:
                if (_skillItems.Count > 0 && _skillSelectedIndex < _skillItems.Count)
                {
                    var item = _skillItems[_skillSelectedIndex];
                    _skillItems[_skillSelectedIndex] = (item.Id, item.Name, !item.Enabled);
                    Draw();
                    AnsiConsole.Flush();
                }
                break;
            case ConsoleKey.Enter:
                SaveBot();
                break;
        }
    }

    private void HandleStepsInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (_stepSelectedIndex > 0) { _stepSelectedIndex--; Draw(); AnsiConsole.Flush(); }
                break;
            case ConsoleKey.DownArrow:
                if (_stepSelectedIndex < _steps.Count - 1) { _stepSelectedIndex++; Draw(); AnsiConsole.Flush(); }
                break;
            case ConsoleKey.A:
                _addingStep = true;
                _newStepBuffer.Clear();
                _newStepCursor = 0;
                Draw();
                AnsiConsole.Flush();
                break;
            case ConsoleKey.D:
            case ConsoleKey.Delete:
                if (_steps.Count > 0 && _stepSelectedIndex < _steps.Count)
                {
                    _steps.RemoveAt(_stepSelectedIndex);
                    if (_stepSelectedIndex >= _steps.Count && _steps.Count > 0)
                        _stepSelectedIndex = _steps.Count - 1;
                    Draw();
                    AnsiConsole.Flush();
                }
                break;
            case ConsoleKey.Enter:
                SaveBot();
                break;
        }
    }

    private void HandleNewStepInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                var text = _newStepBuffer.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _steps.Add(text);
                    _stepSelectedIndex = _steps.Count - 1;
                }
                _addingStep = false;
                _newStepBuffer.Clear();
                _newStepCursor = 0;
                Draw();
                AnsiConsole.Flush();
                break;
            default:
                HandleTextInput(key, _newStepBuffer, ref _newStepCursor);
                break;
        }
    }

    private void HandleTextInput(ConsoleKeyInfo key, StringBuilder buffer, ref int cursor)
    {
        switch (key.Key)
        {
            case ConsoleKey.Backspace:
                if (cursor > 0) { buffer.Remove(cursor - 1, 1); cursor--; }
                Draw();
                AnsiConsole.Flush();
                break;
            case ConsoleKey.Delete:
                if (cursor < buffer.Length) { buffer.Remove(cursor, 1); }
                Draw();
                AnsiConsole.Flush();
                break;
            case ConsoleKey.LeftArrow:
                if (cursor > 0) cursor--;
                Draw();
                AnsiConsole.Flush();
                break;
            case ConsoleKey.RightArrow:
                if (cursor < buffer.Length) cursor++;
                Draw();
                AnsiConsole.Flush();
                break;
            case ConsoleKey.Home:
                cursor = 0;
                Draw();
                AnsiConsole.Flush();
                break;
            case ConsoleKey.End:
                cursor = buffer.Length;
                Draw();
                AnsiConsole.Flush();
                break;
            default:
                if (key.KeyChar >= 32 && key.KeyChar < 127)
                {
                    buffer.Insert(cursor, key.KeyChar);
                    cursor++;
                    Draw();
                    AnsiConsole.Flush();
                }
                break;
        }
    }

    private void SaveBot()
    {
        var label = _labelBuffer.ToString().Trim();
        var goal = _goalBuffer.ToString().Trim();

        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(goal))
        {
            _statusText = "Label and Goal are required.";
            _statusColor = ConsoleColor.Red;
            Draw();
            AnsiConsole.Flush();
            return;
        }

        _busy = true;
        _statusText = "Saving...";
        _statusColor = ConsoleColor.Yellow;
        Draw();
        AnsiConsole.Flush();

        var wasRunning = _botEngine.IsRunning(_bot.Id);
        var (_, scheduleType) = ScheduleOptions[_scheduleIndex];
        var intervalMinutes = 0;
        if (scheduleType == BotScheduleType.Interval)
            int.TryParse(_intervalBuffer.ToString().Trim(), out intervalMinutes);

        Task.Run(async () =>
        {
            try
            {
                if (wasRunning)
                    await _botEngine.StopBotAsync(_bot.Id);

                _bot.Label = label;
                _bot.Goal = goal;
                _bot.Persona = _personaBuffer.Length > 0 ? _personaBuffer.ToString().Trim() : null;
                _bot.ScheduleType = scheduleType;
                _bot.ScheduleIntervalMinutes = intervalMinutes;

                // Save selected skills
                var enabledIds = _skillItems
                    .Where(s => s.Enabled)
                    .Select(s => s.Id);
                _bot.SetEnabledSkillIds(enabledIds);

                await _botStore.UpdateAsync(_bot);

                // Save steps
                var botSteps = new List<BotStep>();
                for (var i = 0; i < _steps.Count; i++)
                {
                    botSteps.Add(new BotStep
                    {
                        BotId = _bot.Id,
                        StepNumber = i + 1,
                        Description = _steps[i]
                    });
                }
                await _botStore.SetStepsAsync(_bot.Id, botSteps);

                if (wasRunning)
                    await _botEngine.StartBotAsync(_bot.Id);

                _app.Post(() =>
                {
                    AnsiConsole.HideCursor();
                    _app.CloseModal();
                    _onUpdated();
                });
            }
            catch (Exception ex)
            {
                _app.Post(() =>
                {
                    _busy = false;
                    _statusText = $"Error: {ex.Message}";
                    _statusColor = ConsoleColor.Red;
                    Draw();
                    AnsiConsole.Flush();
                });
            }
        });
    }
}
