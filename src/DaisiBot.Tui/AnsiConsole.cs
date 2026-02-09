using System.Runtime.InteropServices;

namespace DaisiBot.Tui;

/// <summary>
/// Static helpers for ANSI escape code terminal rendering.
/// All methods write directly to Console.Out.
/// </summary>
public static class AnsiConsole
{
    private const string Esc = "\x1b[";

    // --- Windows VT100 support ---

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(nint handle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(nint handle, uint mode);

    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    public static void EnableWindowsVt100()
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (GetConsoleMode(handle, out var mode))
            SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
    }

    // --- Screen buffer ---

    public static void EnableAlternateBuffer() => Console.Write($"{Esc}?1049h");
    public static void DisableAlternateBuffer() => Console.Write($"{Esc}?1049l");

    // --- Cursor ---

    public static void HideCursor() => Console.Write($"{Esc}?25l");
    public static void ShowCursor() => Console.Write($"{Esc}?25h");
    public static void MoveTo(int row, int col) => Console.Write($"{Esc}{row + 1};{col + 1}H");

    // --- Clear ---

    public static void ClearScreen() => Console.Write($"{Esc}2J{Esc}H");
    public static void ClearLine() => Console.Write($"{Esc}2K");

    public static void ClearRegion(int top, int left, int height, int width)
    {
        var blank = new string(' ', width);
        for (var r = 0; r < height; r++)
        {
            MoveTo(top + r, left);
            Console.Write(blank);
        }
    }

    // --- Writing ---

    public static void WriteAt(int row, int col, string text)
    {
        MoveTo(row, col);
        Console.Write(text);
    }

    public static void WriteAt(int row, int col, string text, int maxWidth)
    {
        MoveTo(row, col);
        if (text.Length > maxWidth)
            Console.Write(text[..maxWidth]);
        else
            Console.Write(text);
    }

    // --- Styles ---

    public static void SetBold() => Console.Write($"{Esc}1m");
    public static void SetDim() => Console.Write($"{Esc}2m");
    public static void SetReverse() => Console.Write($"{Esc}7m");
    public static void SetUnderline() => Console.Write($"{Esc}4m");
    public static void ResetStyle() => Console.Write($"{Esc}0m");

    // --- Colors ---

    public static void SetForeground(ConsoleColor color) => Console.Write($"{Esc}{AnsiColorCode(color, foreground: true)}m");
    public static void SetBackground(ConsoleColor color) => Console.Write($"{Esc}{AnsiColorCode(color, foreground: false)}m");

    public static void SetForegroundRgb(int r, int g, int b) => Console.Write($"{Esc}38;2;{r};{g};{b}m");
    public static void SetBackgroundRgb(int r, int g, int b) => Console.Write($"{Esc}48;2;{r};{g};{b}m");

    private static int AnsiColorCode(ConsoleColor color, bool foreground)
    {
        var baseCode = foreground ? 30 : 40;
        return color switch
        {
            ConsoleColor.Black => baseCode + 0,
            ConsoleColor.DarkRed => baseCode + 1,
            ConsoleColor.DarkGreen => baseCode + 2,
            ConsoleColor.DarkYellow => baseCode + 3,
            ConsoleColor.DarkBlue => baseCode + 4,
            ConsoleColor.DarkMagenta => baseCode + 5,
            ConsoleColor.DarkCyan => baseCode + 6,
            ConsoleColor.Gray => baseCode + 7,
            ConsoleColor.DarkGray => baseCode + 60 + 0,
            ConsoleColor.Red => baseCode + 60 + 1,
            ConsoleColor.Green => baseCode + 60 + 2,
            ConsoleColor.Yellow => baseCode + 60 + 3,
            ConsoleColor.Blue => baseCode + 60 + 4,
            ConsoleColor.Magenta => baseCode + 60 + 5,
            ConsoleColor.Cyan => baseCode + 60 + 6,
            ConsoleColor.White => baseCode + 60 + 7,
            _ => baseCode + 7
        };
    }

    // --- Box drawing ---

    public static void DrawBox(int top, int left, int height, int width)
    {
        // Corners and edges using Unicode box-drawing
        const char tl = '\u250C'; // ┌
        const char tr = '\u2510'; // ┐
        const char bl = '\u2514'; // └
        const char br = '\u2518'; // ┘
        const char h = '\u2500';  // ─
        const char v = '\u2502';  // │

        var hLine = new string(h, width - 2);

        // Top border
        WriteAt(top, left, $"{tl}{hLine}{tr}");

        // Side borders
        for (var r = 1; r < height - 1; r++)
        {
            WriteAt(top + r, left, v.ToString());
            WriteAt(top + r, left + width - 1, v.ToString());
        }

        // Bottom border
        WriteAt(top + height - 1, left, $"{bl}{hLine}{br}");
    }

    public static void DrawHorizontalLine(int row, int col, int width)
    {
        WriteAt(row, col, new string('\u2500', width));
    }

    public static void DrawVerticalLine(int col, int topRow, int height)
    {
        for (var r = 0; r < height; r++)
            WriteAt(topRow + r, col, "\u2502");
    }

    // --- Compound helpers ---

    public static void WriteAtReverse(int row, int col, string text, int fillWidth)
    {
        SetReverse();
        MoveTo(row, col);
        if (text.Length >= fillWidth)
            Console.Write(text[..fillWidth]);
        else
            Console.Write(text + new string(' ', fillWidth - text.Length));
        ResetStyle();
    }

    public static void WriteAtColored(int row, int col, string text, ConsoleColor fg)
    {
        SetForeground(fg);
        WriteAt(row, col, text);
        ResetStyle();
    }

    public static void Flush() => Console.Out.Flush();
}
