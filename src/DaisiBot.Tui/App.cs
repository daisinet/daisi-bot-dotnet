using System.Collections.Concurrent;

namespace DaisiBot.Tui;

/// <summary>
/// Single-threaded event loop that replaces Terminal.Gui's Application.
/// Drains a ConcurrentQueue for thread-safe UI updates from async tasks,
/// polls Console.KeyAvailable, checks for terminal resize, sleeps 16ms per iteration.
/// </summary>
public class App
{
    private readonly ConcurrentQueue<Action> _uiQueue = new();
    private readonly IServiceProvider _services;
    private volatile bool _running;
    private int _lastWidth;
    private int _lastHeight;

    // Modal stack: the topmost entry handles keys and rendering
    private readonly Stack<IModal> _modalStack = new();
    private IScreen? _mainScreen;

    public IServiceProvider Services => _services;
    public int Width => Console.WindowWidth;
    public int Height => Console.WindowHeight;

    /// <summary>The currently active/visible main screen.</summary>
    public IScreen? ActiveScreen => _mainScreen;

    /// <summary>True when a modal dialog is open — screens should defer drawing.</summary>
    public bool IsModalOpen => _modalStack.Count > 0;

    public App(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// Post an action to run on the UI thread (from any thread).
    /// </summary>
    public void Post(Action action) => _uiQueue.Enqueue(action);

    /// <summary>
    /// Push a modal dialog onto the stack. It will receive keys until closed.
    /// </summary>
    public void RunModal(IModal modal)
    {
        _modalStack.Push(modal);
        modal.Draw();
        AnsiConsole.Flush();
    }

    /// <summary>
    /// Pop the current modal and redraw.
    /// </summary>
    public void CloseModal()
    {
        if (_modalStack.Count > 0)
            _modalStack.Pop();

        // Redraw everything underneath
        _mainScreen?.Draw();
        if (_modalStack.Count > 0)
            _modalStack.Peek().Draw();
        AnsiConsole.Flush();
    }

    /// <summary>
    /// Switch to a different main screen.
    /// </summary>
    public void SetScreen(IScreen screen)
    {
        _mainScreen = screen;
        AnsiConsole.ClearScreen();
        _mainScreen.Activate();
        _mainScreen.Draw();
        if (_modalStack.Count > 0)
            _modalStack.Peek().Draw();
        AnsiConsole.Flush();
    }

    /// <summary>
    /// Request the app to quit.
    /// </summary>
    public void Quit() => _running = false;

    /// <summary>
    /// Run the main event loop. Blocks until Quit() is called.
    /// </summary>
    public void Run(IScreen screen)
    {
        _mainScreen = screen;
        _running = true;

        // Setup terminal
        AnsiConsole.EnableWindowsVt100();
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;
        AnsiConsole.EnableAlternateBuffer();
        AnsiConsole.HideCursor();
        Console.CursorVisible = false;

        _lastWidth = Console.WindowWidth;
        _lastHeight = Console.WindowHeight;

        // Initial draw
        AnsiConsole.ClearScreen();
        screen.Activate();
        screen.Draw();
        AnsiConsole.Flush();

        try
        {
            while (_running)
            {
                // 1. Drain UI queue
                while (_uiQueue.TryDequeue(out var action))
                {
                    try { action(); }
                    catch { /* swallow UI callback errors */ }
                }

                // 2. Check for terminal resize
                var w = Console.WindowWidth;
                var h = Console.WindowHeight;
                if (w != _lastWidth || h != _lastHeight)
                {
                    _lastWidth = w;
                    _lastHeight = h;
                    AnsiConsole.ClearScreen();
                    _mainScreen?.Draw();
                    if (_modalStack.Count > 0)
                        _modalStack.Peek().Draw();
                    AnsiConsole.Flush();
                }

                // 3. Poll keyboard
                while (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);

                    if (_modalStack.Count > 0)
                        _modalStack.Peek().HandleKey(key);
                    else
                        _mainScreen?.HandleKey(key);

                    AnsiConsole.Flush();
                }

                // 4. Sleep to avoid busy-wait (~60 checks/sec)
                Thread.Sleep(16);
            }
        }
        finally
        {
            AnsiConsole.ShowCursor();
            AnsiConsole.DisableAlternateBuffer();
            AnsiConsole.ResetStyle();
        }
    }
}

/// <summary>
/// Interface for the main screen that handles keyboard input and rendering.
/// </summary>
public interface IScreen
{
    void Draw();
    void HandleKey(ConsoleKeyInfo key);
    /// <summary>Called when this screen becomes the active screen.</summary>
    void Activate() { }
}

/// <summary>
/// Interface for modal dialogs pushed on top of the main screen.
/// </summary>
public interface IModal
{
    void Draw();
    void HandleKey(ConsoleKeyInfo key);
}
