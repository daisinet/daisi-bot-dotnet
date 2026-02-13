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
        // Word-wrap the message to determine box height
        const int boxWidth = 50;
        const int innerWidth = boxWidth - 4;
        var lines = WordWrap(_message, innerWidth);
        var boxHeight = lines.Count + 5; // 1 top border + lines + 1 blank + 1 buttons + 1 blank + 1 bottom border
        _box = DialogRunner.DrawCenteredBox(_app, "Confirm", boxWidth, boxHeight);

        for (var i = 0; i < lines.Count; i++)
            DialogRunner.DrawLabel(_box, 1 + i, lines[i]);

        // Buttons
        var btnRow = _box.InnerTop + lines.Count + 1;
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

    private static List<string> WordWrap(string text, int maxWidth)
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        var current = "";

        foreach (var word in words)
        {
            if (current.Length == 0)
            {
                current = word;
            }
            else if (current.Length + 1 + word.Length <= maxWidth)
            {
                current += " " + word;
            }
            else
            {
                lines.Add(current);
                current = word;
            }
        }

        if (current.Length > 0)
            lines.Add(current);

        return lines;
    }
}

/// <summary>
/// Three-option host mode picker: SelfHost / DaisiNet / Localhost.
/// </summary>
public class HostModeDialog : IModal
{
    private readonly App _app;
    private readonly Action<int> _callback;
    private DialogRunner.BoxBounds? _box;
    private int _selectedIndex;

    private static readonly string[] Labels = ["SelfHost", "DaisiNet", "Localhost"];
    private static readonly string[] Descriptions =
    [
        "Run inference locally using GGUF models",
        "Use DaisiNet cloud ORC (charges may apply)",
        "Connect to localhost:5001 for debugging"
    ];

    public HostModeDialog(App app, int currentIndex, Action<int> callback)
    {
        _app = app;
        _selectedIndex = currentIndex;
        _callback = callback;
    }

    public void Draw()
    {
        const int boxWidth = 52;
        const int boxHeight = 8;
        _box = DialogRunner.DrawCenteredBox(_app, "Host Mode", boxWidth, boxHeight);

        // Description of selected mode
        var desc = Descriptions[_selectedIndex];
        DialogRunner.DrawLabel(_box, 1, desc.PadRight(_box.InnerWidth));

        // Buttons row
        var btnRow = _box.InnerTop + 3;
        var totalWidth = Labels.Sum(l => l.Length + 4) + (Labels.Length - 1) * 2;
        var btnLeft = _box.InnerLeft + (_box.InnerWidth - totalWidth) / 2;

        for (var i = 0; i < Labels.Length; i++)
        {
            var label = $" {Labels[i]} ";
            if (i == _selectedIndex)
                AnsiConsole.SetReverse();
            AnsiConsole.WriteAt(btnRow, btnLeft, label);
            AnsiConsole.ResetStyle();
            btnLeft += label.Length + 2;
        }

        DialogRunner.DrawButtonHints(_box, " Left/Right:Navigate  Enter:Select  Esc:Cancel ");
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                _selectedIndex = (_selectedIndex + Labels.Length - 1) % Labels.Length;
                Draw();
                break;

            case ConsoleKey.RightArrow:
            case ConsoleKey.Tab:
                _selectedIndex = (_selectedIndex + 1) % Labels.Length;
                Draw();
                break;

            case ConsoleKey.Enter:
                _app.CloseModal();
                _callback(_selectedIndex);
                break;

            case ConsoleKey.Escape:
                _app.CloseModal();
                break;
        }
    }
}
