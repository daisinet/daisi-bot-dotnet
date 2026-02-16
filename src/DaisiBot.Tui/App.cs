using System.Collections.Concurrent;
using System.Runtime.InteropServices;

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

    // Diagnostic log for debugging TUI freezes
    private static readonly string DiagLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DaisiHost", "tui-diag.log");

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

    internal static void DiagLog(string message)
    {
        try
        {
            File.AppendAllText(DiagLogPath,
                $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { /* best effort */ }
    }

    private static void LogException(string context, Exception ex)
    {
        var msg = $"{context}: {ex.GetType().Name}: {ex.Message}\n  {ex.StackTrace}";
        var inner = ex.InnerException;
        while (inner is not null)
        {
            msg += $"\n  --- Inner: {inner.GetType().Name}: {inner.Message}\n  {inner.StackTrace}";
            inner = inner.InnerException;
        }
        DiagLog(msg);
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
        try
        {
            modal.Draw();
            AnsiConsole.Flush();
        }
        catch (Exception ex)
        {
            // If Draw fails, remove the modal so it doesn't invisibly intercept all keys
            DiagLog($"RunModal Draw failed ({modal.GetType().Name}): {ex.Message}");
            _modalStack.Pop();
            _mainScreen?.Draw();
            AnsiConsole.Flush();
        }
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

    // --- Windows console input mode P/Invoke ---

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(nint handle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(nint handle, uint mode);

    private const int STD_INPUT_HANDLE = -10;

    /// <summary>
    /// Ensures the console input handle is in the right mode for raw key polling.
    /// Native libraries (e.g. LlamaSharp) can change console mode as a side-effect.
    /// </summary>
    private static void EnsureRawInputMode()
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = GetStdHandle(STD_INPUT_HANDLE);
        if (GetConsoleMode(handle, out var mode))
        {
            // Disable line input (0x0002) and echo (0x0004) so Console.KeyAvailable works
            const uint ENABLE_LINE_INPUT = 0x0002;
            const uint ENABLE_ECHO_INPUT = 0x0004;
            var newMode = mode & ~ENABLE_LINE_INPUT & ~ENABLE_ECHO_INPUT;
            if (newMode != mode)
            {
                SetConsoleMode(handle, newMode);
            }
        }
    }

    /// <summary>
    /// Run the main event loop. Blocks until Quit() is called.
    /// </summary>
    public void Run(IScreen screen)
    {
        // Truncate previous diagnostic log
        try { File.WriteAllText(DiagLogPath, ""); } catch { }
        DiagLog("=== TUI starting ===");

        _mainScreen = screen;
        _running = true;

        // Setup terminal
        AnsiConsole.EnableWindowsVt100();
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;
        AnsiConsole.EnableAlternateBuffer();
        AnsiConsole.HideCursor();
        Console.CursorVisible = false;

        // Ensure console input is in raw mode (not line-buffered)
        EnsureRawInputMode();

        _lastWidth = Console.WindowWidth;
        _lastHeight = Console.WindowHeight;

        // Initial draw
        AnsiConsole.ClearScreen();
        screen.Activate();
        screen.Draw();
        AnsiConsole.Flush();

        DiagLog("Initial draw complete, entering event loop");

        try
        {
            var loopCount = 0;
            while (_running)
            {
                // 1. Drain UI queue
                while (_uiQueue.TryDequeue(out var action))
                {
                    try { action(); }
                    catch (Exception ex)
                    {
                        LogException("UI queue error", ex);
                    }
                }

                // Re-assert raw input mode after processing UI queue actions,
                // in case a service or native library changed it
                if (loopCount % 60 == 0) // check once per second
                    EnsureRawInputMode();

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

                    try
                    {
                        if (_modalStack.Count > 0)
                        {
                            if (loopCount < 300) // log first ~5 seconds
                                DiagLog($"Key {key.Key} -> modal ({_modalStack.Peek().GetType().Name}), stack={_modalStack.Count}");
                            _modalStack.Peek().HandleKey(key);
                        }
                        else
                        {
                            if (loopCount < 300)
                                DiagLog($"Key {key.Key} -> screen ({_mainScreen?.GetType().Name})");
                            _mainScreen?.HandleKey(key);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogException("HandleKey error", ex);
                    }

                    AnsiConsole.Flush();
                }

                // Log diagnostic info for the first few seconds
                if (loopCount == 0)
                    DiagLog($"First loop iteration, modals={_modalStack.Count}" +
                        (_modalStack.Count > 0 ? $", top={_modalStack.Peek().GetType().Name}" : ""));
                else if (loopCount == 60)
                    DiagLog($"~1s elapsed, modals={_modalStack.Count}" +
                        (_modalStack.Count > 0 ? $", top={_modalStack.Peek().GetType().Name}" : ""));
                else if (loopCount == 300)
                    DiagLog($"~5s elapsed, modals={_modalStack.Count}, diagnostic key logging stopped");

                loopCount++;

                // 4. Sleep to avoid busy-wait (~60 checks/sec)
                Thread.Sleep(16);
            }
        }
        finally
        {
            DiagLog("Event loop exiting");
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
