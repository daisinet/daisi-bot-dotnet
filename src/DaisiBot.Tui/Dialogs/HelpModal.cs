namespace DaisiBot.Tui.Dialogs;

public class HelpModal : IModal
{
    private readonly App _app;
    private DialogRunner.BoxBounds? _box;
    private int _scrollOffset;

    private static readonly string[] HelpLines =
    [
        "Available Commands:",
        "",
        "  /help              Show this help",
        "  /new               Create new bot or conversation",
        "  /kill [label]      Stop a running bot",
        "  /list              List all bots with status",
        "  /status            Show detailed bot status",
        "  /runnow            Run selected bot immediately",
        "  /install <skill>   Search and install a skill",
        "  /skills            Open skill browser",
        "  /model             Open model picker",
        "  /settings          Open settings",
        "  /login             Open login flow",
        "  /clear             Clear current output",
        "  /export [file]     Export log/chat to file",
        "",
        "Keyboard Shortcuts:",
        "",
        "  F1                 Switch to Bots screen",
        "  F2                 Switch to Chats screen",
        "  F3                 Model picker",
        "  F4                 Settings",
        "  F5                 Login",
        "  F6                 Skill browser",
        "  F10                Quit",
        "  Tab                Toggle sidebar/panel focus",
        "  N                  New bot/conversation (sidebar)",
        "  D                  Delete selected (sidebar)",
        "  PageUp/Down        Scroll output",
        "  Esc                Stop running bot",
    ];

    public HelpModal(App app)
    {
        _app = app;
    }

    public void Draw()
    {
        var height = Math.Min(HelpLines.Length + 4, _app.Height - 4);
        _box = DialogRunner.DrawCenteredBox(_app, "Help", 54, height);

        var visibleLines = _box.InnerHeight - 1;

        if (_scrollOffset > Math.Max(0, HelpLines.Length - visibleLines))
            _scrollOffset = Math.Max(0, HelpLines.Length - visibleLines);

        for (var i = 0; i < visibleLines; i++)
        {
            var lineIdx = _scrollOffset + i;
            var row = _box.InnerTop + i;

            if (lineIdx < HelpLines.Length)
            {
                var line = HelpLines[lineIdx];
                if (line.Length > _box.InnerWidth)
                    line = line[.._box.InnerWidth];
                AnsiConsole.WriteAt(row, _box.InnerLeft, line.PadRight(_box.InnerWidth));
            }
            else
            {
                AnsiConsole.WriteAt(row, _box.InnerLeft, new string(' ', _box.InnerWidth));
            }
        }

        DialogRunner.DrawButtonHints(_box, " Esc:Close  PgUp/PgDn:Scroll ");
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Escape:
                _app.CloseModal();
                break;
            case ConsoleKey.PageUp:
                _scrollOffset = Math.Max(0, _scrollOffset - 10);
                Draw();
                break;
            case ConsoleKey.PageDown:
                _scrollOffset += 10;
                Draw();
                break;
            case ConsoleKey.UpArrow:
                if (_scrollOffset > 0) { _scrollOffset--; Draw(); }
                break;
            case ConsoleKey.DownArrow:
                _scrollOffset++;
                Draw();
                break;
        }
    }
}
