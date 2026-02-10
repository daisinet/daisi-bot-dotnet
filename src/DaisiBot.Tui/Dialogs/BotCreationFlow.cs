using System.Text;
using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Dialogs;

public class BotCreationFlow : IModal
{
    private readonly App _app;
    private readonly IBotStore _botStore;
    private readonly IBotEngine _botEngine;
    private readonly ISettingsService _settingsService;
    private readonly Action<BotInstance> _onCreated;
    private DialogRunner.BoxBounds? _box;

    private enum Step { Goal, Label, Persona, Schedule, IntervalInput, StartMode }
    private Step _step = Step.Goal;

    private readonly StringBuilder _goalBuffer = new();
    private readonly StringBuilder _labelBuffer = new();
    private readonly StringBuilder _personaBuffer = new();
    private readonly StringBuilder _intervalBuffer = new();
    private int _goalCursor;
    private int _labelCursor;
    private int _personaCursor;
    private int _intervalCursor;
    private int _scheduleIndex;
    private int _startModeIndex;
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

    public BotCreationFlow(App app, IServiceProvider services, Action<BotInstance> onCreated)
    {
        _app = app;
        _botStore = services.GetRequiredService<IBotStore>();
        _botEngine = services.GetRequiredService<IBotEngine>();
        _settingsService = services.GetRequiredService<ISettingsService>();
        _onCreated = onCreated;
    }

    public void Draw()
    {
        var needsInterval = SelectedScheduleType == BotScheduleType.Interval && _step >= Step.IntervalInput;
        var boxHeight = needsInterval ? 25 : 23;
        _box = DialogRunner.DrawCenteredBox(_app, "Create Bot", 56, boxHeight);

        // Step 1: Goal
        DialogRunner.DrawLabel(_box, 0, "What should this bot do?");
        DialogRunner.DrawTextField(_box, 1, _goalBuffer.ToString(), _step == Step.Goal && !_busy);

        // Step 2: Label
        if (_step >= Step.Label)
        {
            DialogRunner.DrawLabel(_box, 3, "Give this bot a label:");
            DialogRunner.DrawTextField(_box, 4, _labelBuffer.ToString(), _step == Step.Label && !_busy);
        }
        else
        {
            DrawEmptyRow(3);
            DrawEmptyRow(4);
        }

        // Step 3: Persona
        if (_step >= Step.Persona)
        {
            DialogRunner.DrawLabel(_box, 6, "Persona (optional, Enter to skip):");
            DialogRunner.DrawTextField(_box, 7, _personaBuffer.ToString(), _step == Step.Persona && !_busy);
        }
        else
        {
            DrawEmptyRow(6);
            DrawEmptyRow(7);
        }

        // Step 4: Schedule
        if (_step >= Step.Schedule)
        {
            DialogRunner.DrawLabel(_box, 9, "How often should this run?");
            for (var i = 0; i < ScheduleOptions.Length; i++)
            {
                var row = 10 + i;
                if (row >= _box.InnerHeight - 2) break;
                var prefix = i == _scheduleIndex ? "> " : "  ";
                var text = $"{prefix}{ScheduleOptions[i].Label}";
                if (i == _scheduleIndex && _step == Step.Schedule)
                {
                    AnsiConsole.SetReverse();
                    AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, text.PadRight(_box.InnerWidth));
                    AnsiConsole.ResetStyle();
                }
                else
                {
                    AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, text.PadRight(_box.InnerWidth));
                }
            }
        }

        var nextRow = 10 + ScheduleOptions.Length;

        // Step 4b: Interval minutes input (only for Interval schedule)
        if (needsInterval)
        {
            nextRow++;
            DialogRunner.DrawLabel(_box, nextRow, "Minutes between runs:");
            nextRow++;
            DialogRunner.DrawTextField(_box, nextRow, _intervalBuffer.ToString(), _step == Step.IntervalInput && !_busy);
            nextRow++;
        }

        // Step 5: Start mode
        if (_step >= Step.StartMode)
        {
            nextRow++;
            DialogRunner.DrawLabel(_box, nextRow, "When should the first run start?");
            var options = new[] { "Run Immediately", GetScheduledLabel() };
            for (var i = 0; i < options.Length; i++)
            {
                var row = nextRow + 1 + i;
                if (row >= _box.InnerHeight - 2) break;
                var prefix = i == _startModeIndex ? "> " : "  ";
                var text = $"{prefix}{options[i]}";
                if (i == _startModeIndex)
                {
                    AnsiConsole.SetReverse();
                    AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, text.PadRight(_box.InnerWidth));
                    AnsiConsole.ResetStyle();
                }
                else
                {
                    AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, text.PadRight(_box.InnerWidth));
                }
            }
        }

        if (_statusText.Length > 0)
            DialogRunner.DrawStatus(_box, _statusText, _statusColor);

        var hints = _step switch
        {
            Step.Goal => " Enter:Next  Esc:Cancel ",
            Step.Label => " Enter:Next  Esc:Cancel ",
            Step.Persona => " Enter:Next/Skip  Esc:Cancel ",
            Step.Schedule => " Enter:Next  Esc:Cancel ",
            Step.IntervalInput => " Enter:Next  Esc:Cancel ",
            Step.StartMode => " Enter:Create  Esc:Cancel ",
            _ => " Esc:Close "
        };
        DialogRunner.DrawButtonHints(_box, hints);

        // Cursor
        if (!_busy)
        {
            AnsiConsole.ShowCursor();
            switch (_step)
            {
                case Step.Goal:
                    AnsiConsole.MoveTo(_box.InnerTop + 1, _box.InnerLeft + _goalCursor);
                    break;
                case Step.Label:
                    AnsiConsole.MoveTo(_box.InnerTop + 4, _box.InnerLeft + _labelCursor);
                    break;
                case Step.Persona:
                    AnsiConsole.MoveTo(_box.InnerTop + 7, _box.InnerLeft + _personaCursor);
                    break;
                case Step.IntervalInput:
                    var intervalFieldRow = 10 + ScheduleOptions.Length + 2;
                    AnsiConsole.MoveTo(_box.InnerTop + intervalFieldRow, _box.InnerLeft + _intervalCursor);
                    break;
                case Step.Schedule:
                case Step.StartMode:
                    AnsiConsole.HideCursor();
                    break;
            }
        }
    }

    private void DrawEmptyRow(int row)
    {
        if (_box is null) return;
        AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, new string(' ', _box.InnerWidth));
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        if (_busy) return;

        if (key.Key == ConsoleKey.Escape)
        {
            AnsiConsole.HideCursor();
            _app.CloseModal();
            return;
        }

        switch (_step)
        {
            case Step.Goal: HandleGoalInput(key); break;
            case Step.Label: HandleLabelInput(key); break;
            case Step.Persona: HandlePersonaInput(key); break;
            case Step.Schedule: HandleScheduleInput(key); break;
            case Step.IntervalInput: HandleIntervalInput(key); break;
            case Step.StartMode: HandleStartModeInput(key); break;
        }
    }

    private void HandleGoalInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                var goal = _goalBuffer.ToString().Trim();
                if (string.IsNullOrWhiteSpace(goal)) return;
                _step = Step.Label;
                // Auto-suggest label from goal
                if (_labelBuffer.Length == 0)
                {
                    var suggested = goal.Length > 30 ? goal[..30] : goal;
                    _labelBuffer.Append(suggested);
                    _labelCursor = _labelBuffer.Length;
                }
                Draw();
                break;
            default:
                HandleTextInput(key, _goalBuffer, ref _goalCursor);
                break;
        }
    }

    private void HandleLabelInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                var label = _labelBuffer.ToString().Trim();
                if (string.IsNullOrWhiteSpace(label)) return;
                _step = Step.Persona;
                Draw();
                break;
            default:
                HandleTextInput(key, _labelBuffer, ref _labelCursor);
                break;
        }
    }

    private void HandlePersonaInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                _step = Step.Schedule;
                Draw();
                break;
            default:
                HandleTextInput(key, _personaBuffer, ref _personaCursor);
                break;
        }
    }

    private BotScheduleType SelectedScheduleType => ScheduleOptions[_scheduleIndex].Type;

    private void HandleScheduleInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (_scheduleIndex > 0) { _scheduleIndex--; Draw(); }
                break;
            case ConsoleKey.DownArrow:
                if (_scheduleIndex < ScheduleOptions.Length - 1) { _scheduleIndex++; Draw(); }
                break;
            case ConsoleKey.Enter:
                if (SelectedScheduleType == BotScheduleType.Interval)
                {
                    _step = Step.IntervalInput;
                    if (_intervalBuffer.Length == 0)
                    {
                        _intervalBuffer.Append("5");
                        _intervalCursor = _intervalBuffer.Length;
                    }
                }
                else
                {
                    _startModeIndex = 0;
                    _step = Step.StartMode;
                }
                Draw();
                break;
        }
    }

    private void HandleIntervalInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                var text = _intervalBuffer.ToString().Trim();
                if (!int.TryParse(text, out var mins) || mins < 1)
                {
                    _statusText = "Enter a number >= 1";
                    _statusColor = ConsoleColor.Red;
                    Draw();
                    return;
                }
                _statusText = "";
                _startModeIndex = 0;
                _step = Step.StartMode;
                Draw();
                break;
            default:
                // Only allow digits
                if (key.KeyChar >= '0' && key.KeyChar <= '9')
                {
                    _intervalBuffer.Insert(_intervalCursor, key.KeyChar);
                    _intervalCursor++;
                    Draw();
                }
                else
                {
                    HandleTextInput(key, _intervalBuffer, ref _intervalCursor);
                }
                break;
        }
    }

    private void HandleStartModeInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (_startModeIndex > 0) { _startModeIndex--; Draw(); }
                break;
            case ConsoleKey.DownArrow:
                if (_startModeIndex < 1) { _startModeIndex++; Draw(); }
                break;
            case ConsoleKey.Enter:
                CreateBot();
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
                break;
            case ConsoleKey.Delete:
                if (cursor < buffer.Length) { buffer.Remove(cursor, 1); }
                Draw();
                break;
            case ConsoleKey.LeftArrow:
                if (cursor > 0) cursor--;
                Draw();
                break;
            case ConsoleKey.RightArrow:
                if (cursor < buffer.Length) cursor++;
                Draw();
                break;
            case ConsoleKey.Home:
                cursor = 0;
                Draw();
                break;
            case ConsoleKey.End:
                cursor = buffer.Length;
                Draw();
                break;
            default:
                if (key.KeyChar >= 32 && key.KeyChar < 127)
                {
                    buffer.Insert(cursor, key.KeyChar);
                    cursor++;
                    Draw();
                }
                break;
        }
    }

    private string GetScheduledLabel()
    {
        var (_, scheduleType) = ScheduleOptions[_scheduleIndex];
        return scheduleType switch
        {
            BotScheduleType.Once => "Don't Start Yet",
            BotScheduleType.Continuous => "Run Immediately",
            _ => "Wait for First Scheduled Time"
        };
    }

    private void CreateBot()
    {
        _busy = true;
        _statusText = "Creating bot...";
        _statusColor = ConsoleColor.Yellow;
        Draw();
        AnsiConsole.Flush();

        var (_, scheduleType) = ScheduleOptions[_scheduleIndex];
        var intervalMinutes = 0;
        if (scheduleType == BotScheduleType.Interval)
            int.TryParse(_intervalBuffer.ToString().Trim(), out intervalMinutes);
        var runImmediately = _startModeIndex == 0;

        Task.Run(async () =>
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                var bot = new BotInstance
                {
                    Goal = _goalBuffer.ToString().Trim(),
                    Label = _labelBuffer.ToString().Trim(),
                    Persona = _personaBuffer.Length > 0 ? _personaBuffer.ToString().Trim() : null,
                    ScheduleType = scheduleType,
                    ScheduleIntervalMinutes = intervalMinutes,
                    ModelName = settings.DefaultModelName,
                    Temperature = settings.Temperature,
                    MaxTokens = settings.MaxTokens,
                    EnabledSkillIdsCsv = settings.EnabledSkillIdsCsv,
                };

                if (runImmediately)
                {
                    bot.Status = BotStatus.Running;
                    bot.NextRunAt = DateTime.UtcNow;
                }
                else
                {
                    bot.Status = scheduleType == BotScheduleType.Once ? BotStatus.Idle : BotStatus.Running;
                    bot.NextRunAt = scheduleType switch
                    {
                        BotScheduleType.Interval => DateTime.UtcNow.AddMinutes(intervalMinutes),
                        BotScheduleType.Hourly => DateTime.UtcNow.AddHours(1),
                        BotScheduleType.Daily => DateTime.UtcNow.AddDays(1),
                        _ => null
                    };
                }

                await _botStore.CreateAsync(bot);

                if (runImmediately)
                    await _botEngine.StartBotAsync(bot.Id);

                _app.Post(() =>
                {
                    AnsiConsole.HideCursor();
                    _app.CloseModal();
                    _onCreated(bot);
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
