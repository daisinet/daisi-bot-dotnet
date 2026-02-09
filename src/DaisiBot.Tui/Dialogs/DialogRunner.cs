namespace DaisiBot.Tui.Dialogs;

/// <summary>
/// Shared dialog primitives: centered boxes, inline text input, list selection, confirmation.
/// </summary>
public static class DialogRunner
{
    public record BoxBounds(int Top, int Left, int Width, int Height)
    {
        public int InnerTop => Top + 1;
        public int InnerLeft => Left + 2;
        public int InnerWidth => Width - 4;
        public int InnerHeight => Height - 2;
    }

    /// <summary>
    /// Draw a centered box with a title. Returns the inner bounds.
    /// </summary>
    public static BoxBounds DrawCenteredBox(App app, string title, int width, int height)
    {
        var left = (app.Width - width) / 2;
        var top = (app.Height - height) / 2;

        // Clear area behind box
        AnsiConsole.ClearRegion(top, left, height, width);

        // Draw border
        AnsiConsole.DrawBox(top, left, height, width);

        // Draw title
        if (title.Length > 0)
        {
            var titleText = $" {title} ";
            if (titleText.Length > width - 4)
                titleText = titleText[..(width - 4)];
            var titleStart = left + (width - titleText.Length) / 2;
            AnsiConsole.SetBold();
            AnsiConsole.WriteAt(top, titleStart, titleText);
            AnsiConsole.ResetStyle();
        }

        return new BoxBounds(top, left, width, height);
    }

    /// <summary>
    /// Draw a label at a position within the dialog.
    /// </summary>
    public static void DrawLabel(BoxBounds box, int row, string text)
    {
        AnsiConsole.WriteAt(box.InnerTop + row, box.InnerLeft,
            text.Length > box.InnerWidth ? text[..box.InnerWidth] : text);
    }

    /// <summary>
    /// Draw a text field (highlighted background) at a position, showing current value.
    /// </summary>
    public static void DrawTextField(BoxBounds box, int row, string value, bool focused)
    {
        var displayWidth = box.InnerWidth;
        var display = value.Length > displayWidth ? value[^displayWidth..] : value.PadRight(displayWidth);

        if (focused)
            AnsiConsole.SetReverse();

        AnsiConsole.WriteAt(box.InnerTop + row, box.InnerLeft, display);

        if (focused)
            AnsiConsole.ResetStyle();
    }

    /// <summary>
    /// Draw a status/message line at the bottom of the dialog.
    /// </summary>
    public static void DrawStatus(BoxBounds box, string text, ConsoleColor color = ConsoleColor.Gray)
    {
        var row = box.Top + box.Height - 2;
        AnsiConsole.SetForeground(color);
        var padded = text.PadRight(box.InnerWidth);
        AnsiConsole.WriteAt(row, box.InnerLeft,
            padded.Length > box.InnerWidth ? padded[..box.InnerWidth] : padded);
        AnsiConsole.ResetStyle();
    }

    /// <summary>
    /// Draw button hints at the bottom of the box border.
    /// </summary>
    public static void DrawButtonHints(BoxBounds box, string hints)
    {
        var row = box.Top + box.Height - 1;
        var start = box.Left + (box.Width - hints.Length) / 2;
        AnsiConsole.SetDim();
        AnsiConsole.WriteAt(row, start, hints);
        AnsiConsole.ResetStyle();
    }
}

/// <summary>
/// Simple Yes/No confirmation dialog.
/// </summary>
public class ConfirmDialog : IModal
{
    private readonly App _app;
    private readonly string _message;
    private readonly Action<bool> _callback;
    private DialogRunner.BoxBounds? _box;
    private bool _selectedYes = true;

    public ConfirmDialog(App app, string message, Action<bool> callback)
    {
        _app = app;
        _message = message;
        _callback = callback;
    }

    public void Draw()
    {
        _box = DialogRunner.DrawCenteredBox(_app, "Confirm", 40, 8);
        DialogRunner.DrawLabel(_box, 1, _message);

        // Buttons
        var btnRow = _box.InnerTop + 3;
        var btnLeft = _box.InnerLeft + 4;

        if (_selectedYes)
            AnsiConsole.SetReverse();
        AnsiConsole.WriteAt(btnRow, btnLeft, " Yes ");
        AnsiConsole.ResetStyle();

        if (!_selectedYes)
            AnsiConsole.SetReverse();
        AnsiConsole.WriteAt(btnRow, btnLeft + 10, " No ");
        AnsiConsole.ResetStyle();

        DialogRunner.DrawButtonHints(_box, " Enter:Select  Esc:Cancel ");
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
            case ConsoleKey.RightArrow:
            case ConsoleKey.Tab:
                _selectedYes = !_selectedYes;
                Draw();
                break;

            case ConsoleKey.Enter:
                _app.CloseModal();
                _callback(_selectedYes);
                break;

            case ConsoleKey.Escape:
                _app.CloseModal();
                _callback(false);
                break;

            case ConsoleKey.Y:
                _app.CloseModal();
                _callback(true);
                break;

            case ConsoleKey.N:
                _app.CloseModal();
                _callback(false);
                break;
        }
    }
}
