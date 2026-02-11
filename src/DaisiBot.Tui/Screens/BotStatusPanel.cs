using DaisiBot.Core.Enums;
using DaisiBot.Core.Models;

namespace DaisiBot.Tui.Screens;

public class BotStatusPanel
{
    private ActionPlan? _plan;
    private BotInstance? _bot;

    public int Top { get; set; }
    public int Left { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsVisible { get; set; }

    public void SetBot(BotInstance? bot)
    {
        _bot = bot;
    }

    public void SetPlan(ActionPlan? plan)
    {
        _plan = plan;
    }

    public void Clear()
    {
        if (Width <= 0 || Height <= 0) return;
        AnsiConsole.ClearRegion(Top, Left, Height, Width);
    }

    public void Draw()
    {
        if (!IsVisible || Width <= 2 || Height <= 2) return;

        var contentWidth = Width - 2;

        // Top border
        AnsiConsole.SetForeground(ConsoleColor.DarkCyan);
        AnsiConsole.WriteAt(Top, Left, "\u250C");
        AnsiConsole.WriteAt(Top, Left + 1, new string('\u2500', contentWidth));
        AnsiConsole.WriteAt(Top, Left + Width - 1, "\u2510");
        AnsiConsole.ResetStyle();

        // Title
        var title = " Status ";
        var titleStart = Left + (Width - title.Length) / 2;
        AnsiConsole.SetForeground(ConsoleColor.Cyan);
        AnsiConsole.SetBold();
        AnsiConsole.WriteAt(Top, titleStart, title);
        AnsiConsole.ResetStyle();

        // Content area
        var listHeight = Height - 2;
        var contentRow = 0;

        // --- Bot status info section ---
        if (_bot is not null)
        {
            // Status line
            if (contentRow < listHeight)
            {
                var (statusLabel, statusColor) = GetBotStatusDisplay(_bot.Status);
                DrawBorderLeft(Top + 1 + contentRow);
                AnsiConsole.SetDim();
                AnsiConsole.WriteAt(Top + 1 + contentRow, Left + 1, " Status: ".PadRight(contentWidth));
                AnsiConsole.ResetStyle();
                AnsiConsole.SetForeground(statusColor);
                AnsiConsole.SetBold();
                AnsiConsole.WriteAt(Top + 1 + contentRow, Left + 10, statusLabel);
                AnsiConsole.ResetStyle();
                DrawBorderRight(Top + 1 + contentRow);
                contentRow++;
            }

            // Schedule type
            if (contentRow < listHeight)
            {
                var schedLabel = _bot.ScheduleType.ToString();
                if (_bot.ScheduleType == BotScheduleType.Interval)
                    schedLabel = $"Every {_bot.ScheduleIntervalMinutes}m";
                DrawContentLine(Top + 1 + contentRow, contentWidth, " Schedule", schedLabel);
                contentRow++;
            }

            // Runs
            if (contentRow < listHeight)
            {
                DrawContentLine(Top + 1 + contentRow, contentWidth, " Runs", _bot.ExecutionCount.ToString());
                contentRow++;
            }

            // Last run
            if (contentRow < listHeight)
            {
                var lastRun = _bot.LastRunAt.HasValue
                    ? FormatRelativeTime(_bot.LastRunAt.Value)
                    : "--";
                DrawContentLine(Top + 1 + contentRow, contentWidth, " Last run", lastRun);
                contentRow++;
            }

            // Next run countdown
            if (contentRow < listHeight)
            {
                var nextRun = "--";
                if (_bot.NextRunAt.HasValue && _bot.NextRunAt.Value > DateTime.UtcNow)
                    nextRun = FormatCountdown(_bot.NextRunAt.Value);
                else if (_bot.Status == BotStatus.Running)
                    nextRun = "now";
                DrawContentLine(Top + 1 + contentRow, contentWidth, " Next run", nextRun);
                contentRow++;
            }

            // Padding line
            if (contentRow < listHeight)
            {
                DrawEmptyLine(Top + 1 + contentRow, contentWidth);
                contentRow++;
            }
        }

        // --- Steps subheading ---
        if (contentRow < listHeight)
        {
            DrawBorderLeft(Top + 1 + contentRow);
            // Horizontal divider with " Steps " label
            AnsiConsole.SetForeground(ConsoleColor.DarkCyan);
            AnsiConsole.WriteAt(Top + 1 + contentRow, Left + 1, new string('\u2500', contentWidth));
            AnsiConsole.ResetStyle();
            var stepsLabel = " Steps ";
            var stepsLabelStart = Left + (Width - stepsLabel.Length) / 2;
            AnsiConsole.SetForeground(ConsoleColor.Cyan);
            AnsiConsole.SetBold();
            AnsiConsole.WriteAt(Top + 1 + contentRow, stepsLabelStart, stepsLabel);
            AnsiConsole.ResetStyle();
            DrawBorderRight(Top + 1 + contentRow);
            contentRow++;
        }

        // --- Step items ---
        var stepIndex = 0;
        while (contentRow < listHeight)
        {
            var row = Top + 1 + contentRow;

            DrawBorderLeft(row);

            if (_plan is not null && stepIndex < _plan.Steps.Count)
            {
                var step = _plan.Steps[stepIndex];
                var (icon, color) = GetStepDisplay(step.Status);
                var desc = step.Description;
                var maxDesc = contentWidth - 3; // icon + space + text
                if (desc.Length > maxDesc) desc = desc[..(maxDesc - 2)] + "..";

                AnsiConsole.SetForeground(color);
                AnsiConsole.WriteAt(row, Left + 1, $" {icon} ");
                AnsiConsole.ResetStyle();

                if (step.Status is ActionItemStatus.Pending or ActionItemStatus.Skipped)
                    AnsiConsole.SetDim();
                else if (step.Status == ActionItemStatus.Running)
                {
                    AnsiConsole.SetForeground(ConsoleColor.Yellow);
                    AnsiConsole.SetBold();
                }
                else if (step.Status == ActionItemStatus.Complete)
                    AnsiConsole.SetForeground(ConsoleColor.Green);
                else if (step.Status == ActionItemStatus.Failed)
                    AnsiConsole.SetForeground(ConsoleColor.Red);

                AnsiConsole.WriteAt(row, Left + 4, desc.PadRight(contentWidth - 3));
                AnsiConsole.ResetStyle();
                stepIndex++;
            }
            else
            {
                AnsiConsole.WriteAt(row, Left + 1, new string(' ', contentWidth));
            }

            DrawBorderRight(row);
            contentRow++;
        }

        // Bottom border
        var bottomRow = Top + Height - 1;
        AnsiConsole.SetForeground(ConsoleColor.DarkCyan);
        AnsiConsole.WriteAt(bottomRow, Left, "\u2514");
        AnsiConsole.WriteAt(bottomRow, Left + 1, new string('\u2500', contentWidth));
        AnsiConsole.WriteAt(bottomRow, Left + Width - 1, "\u2518");
        AnsiConsole.ResetStyle();
    }

    private void DrawBorderLeft(int row)
    {
        AnsiConsole.SetForeground(ConsoleColor.DarkCyan);
        AnsiConsole.WriteAt(row, Left, "\u2502");
        AnsiConsole.ResetStyle();
    }

    private void DrawBorderRight(int row)
    {
        AnsiConsole.SetForeground(ConsoleColor.DarkCyan);
        AnsiConsole.WriteAt(row, Left + Width - 1, "\u2502");
        AnsiConsole.ResetStyle();
    }

    private void DrawEmptyLine(int row, int contentWidth)
    {
        DrawBorderLeft(row);
        AnsiConsole.WriteAt(row, Left + 1, new string(' ', contentWidth));
        DrawBorderRight(row);
    }

    private void DrawContentLine(int row, int contentWidth, string label, string value)
    {
        DrawBorderLeft(row);
        var full = $"{label}: ";
        AnsiConsole.SetDim();
        AnsiConsole.WriteAt(row, Left + 1, full.PadRight(contentWidth));
        AnsiConsole.ResetStyle();
        var valueCol = Left + 1 + full.Length;
        var maxVal = contentWidth - full.Length;
        if (value.Length > maxVal) value = value[..(maxVal - 2)] + "..";
        AnsiConsole.SetForeground(ConsoleColor.White);
        AnsiConsole.WriteAt(row, valueCol, value);
        AnsiConsole.ResetStyle();
        DrawBorderRight(row);
    }

    private static (string Label, ConsoleColor Color) GetBotStatusDisplay(BotStatus status) => status switch
    {
        BotStatus.Idle => ("Idle", ConsoleColor.Gray),
        BotStatus.Running => ("Running", ConsoleColor.Green),
        BotStatus.WaitingForInput => ("Awaiting Input", ConsoleColor.Yellow),
        BotStatus.Completed => ("Completed", ConsoleColor.Cyan),
        BotStatus.Failed => ("Failed", ConsoleColor.Red),
        BotStatus.Stopped => ("Stopped", ConsoleColor.DarkYellow),
        _ => (status.ToString(), ConsoleColor.Gray)
    };

    private static string FormatRelativeTime(DateTime utcTime)
    {
        var elapsed = DateTime.UtcNow - utcTime;
        if (elapsed.TotalSeconds < 60) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
        return $"{(int)elapsed.TotalDays}d ago";
    }

    private static string FormatCountdown(DateTime utcTarget)
    {
        var remaining = utcTarget - DateTime.UtcNow;
        if (remaining.TotalSeconds <= 0) return "now";
        if (remaining.TotalSeconds < 60) return $"{(int)remaining.TotalSeconds}s";
        if (remaining.TotalMinutes < 60) return $"{(int)remaining.TotalMinutes}m {remaining.Seconds}s";
        if (remaining.TotalHours < 24) return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
        return $"{(int)remaining.TotalDays}d {remaining.Hours}h";
    }

    private static (string Icon, ConsoleColor Color) GetStepDisplay(ActionItemStatus status) => status switch
    {
        ActionItemStatus.Pending => ("\u2610", ConsoleColor.Gray),       // ☐
        ActionItemStatus.Running => ("\u25B6", ConsoleColor.Yellow),     // ▶
        ActionItemStatus.Complete => ("\u2611", ConsoleColor.Green),     // ☑
        ActionItemStatus.Failed => ("\u2717", ConsoleColor.Red),         // ✗
        ActionItemStatus.Skipped => ("\u2014", ConsoleColor.Gray),       // —
        _ => (" ", ConsoleColor.Gray)
    };
}
