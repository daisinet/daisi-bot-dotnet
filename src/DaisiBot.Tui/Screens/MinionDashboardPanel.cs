using DaisiBot.Agent.Minion;

namespace DaisiBot.Tui.Screens;

/// <summary>
/// Dashboard panel showing spawned minion status above the content area.
/// Drawn when the summoner has active minions.
/// </summary>
public class MinionDashboardPanel
{
    private MinionProcessManager? _processManager;

    public int Top { get; set; }
    public int Left { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsVisible { get; set; }

    public void SetProcessManager(MinionProcessManager? manager)
    {
        _processManager = manager;
        IsVisible = manager is not null;
    }

    public int RequiredHeight
    {
        get
        {
            if (_processManager is null || _processManager.Minions.Count == 0)
                return 0;
            return _processManager.Minions.Count + 2; // border top + items + border bottom
        }
    }

    public void Draw()
    {
        if (!IsVisible || _processManager is null || Width < 10 || Height < 3)
            return;

        var minions = _processManager.Minions;
        if (minions.Count == 0) return;

        var contentWidth = Width - 2;

        // Top border
        AnsiConsole.SetForeground(ConsoleColor.DarkMagenta);
        AnsiConsole.WriteAt(Top, Left, "\u250C");
        AnsiConsole.WriteAt(Top, Left + 1, new string('\u2500', contentWidth));
        AnsiConsole.WriteAt(Top, Left + Width - 1, "\u2510");

        // Title
        var title = " Minions ";
        var titleStart = Left + (Width - title.Length) / 2;
        AnsiConsole.SetForeground(ConsoleColor.Magenta);
        AnsiConsole.SetBold();
        AnsiConsole.WriteAt(Top, titleStart, title);
        AnsiConsole.ResetStyle();

        // Minion entries
        var row = 1;
        foreach (var (id, info) in minions)
        {
            if (row >= Height - 1) break;

            var drawRow = Top + row;

            // Left border
            AnsiConsole.SetForeground(ConsoleColor.DarkMagenta);
            AnsiConsole.WriteAt(drawRow, Left, "\u2502");
            AnsiConsole.ResetStyle();

            // Status icon
            var (icon, color) = info.Status switch
            {
                MinionStatus.Running => ("\u25CF", ConsoleColor.Green),    // ●
                MinionStatus.Complete => ("\u2713", ConsoleColor.Cyan),    // ✓
                MinionStatus.Failed => ("\u2717", ConsoleColor.Red),       // ✗
                MinionStatus.Stopped => ("\u25A0", ConsoleColor.Yellow),   // ■
                _ => ("\u25CB", ConsoleColor.Gray)                          // ○
            };

            AnsiConsole.SetForeground(color);
            AnsiConsole.WriteAt(drawRow, Left + 2, icon);
            AnsiConsole.ResetStyle();

            // ID
            AnsiConsole.SetForeground(ConsoleColor.White);
            AnsiConsole.SetBold();
            AnsiConsole.WriteAt(drawRow, Left + 4, id);
            AnsiConsole.ResetStyle();

            // Status
            var statusText = info.Status switch
            {
                MinionStatus.Complete => "complete",
                MinionStatus.Failed => "failed",
                MinionStatus.Stopped => "stopped",
                _ => "working"
            };
            var elapsed = (info.CompletedAt ?? DateTime.UtcNow) - info.StartedAt;
            var statusStr = $"[{statusText} {elapsed.TotalSeconds:F0}s]";
            var statusCol = Left + 4 + id.Length + 1;
            AnsiConsole.SetDim();
            AnsiConsole.WriteAt(drawRow, statusCol, statusStr);
            AnsiConsole.ResetStyle();

            // Goal (truncated)
            var goalCol = statusCol + statusStr.Length + 1;
            var maxGoal = Left + Width - 1 - goalCol;
            if (maxGoal > 0)
            {
                var goal = info.Goal;
                if (goal.Length > maxGoal) goal = goal[..(maxGoal - 2)] + "..";
                AnsiConsole.SetForeground(ConsoleColor.Gray);
                AnsiConsole.WriteAt(drawRow, goalCol, goal);
                AnsiConsole.ResetStyle();
            }

            // Pad remaining space
            var written = goalCol + Math.Min(info.Goal.Length, maxGoal > 0 ? maxGoal : 0) - (Left + 1);
            if (written < contentWidth)
                AnsiConsole.WriteAt(drawRow, Left + 1 + written, new string(' ', contentWidth - written));

            // Right border
            AnsiConsole.SetForeground(ConsoleColor.DarkMagenta);
            AnsiConsole.WriteAt(drawRow, Left + Width - 1, "\u2502");
            AnsiConsole.ResetStyle();

            row++;
        }

        // Bottom border
        var bottomRow = Top + Math.Min(row, Height - 1);
        AnsiConsole.SetForeground(ConsoleColor.DarkMagenta);
        AnsiConsole.WriteAt(bottomRow, Left, "\u2514");
        AnsiConsole.WriteAt(bottomRow, Left + 1, new string('\u2500', contentWidth));
        AnsiConsole.WriteAt(bottomRow, Left + Width - 1, "\u2518");
        AnsiConsole.ResetStyle();
    }
}
