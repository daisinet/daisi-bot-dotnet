using System.Text;
using DaisiBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Dialogs;

/// <summary>
/// Two-step auth flow: email → code → welcome.
/// Runs as a modal dialog with async operations posted back to UI thread.
/// </summary>
public class LoginFlow : IModal
{
    private readonly App _app;
    private readonly IAuthService _authService;
    private DialogRunner.BoxBounds? _box;

    private enum Step { Email, Code, Done }
    private Step _step = Step.Email;

    private readonly StringBuilder _emailBuffer = new();
    private readonly StringBuilder _codeBuffer = new();
    private int _emailCursor;
    private int _codeCursor;
    private string _statusText = "";
    private ConsoleColor _statusColor = ConsoleColor.Gray;
    private string _emailOrPhone = "";
    private bool _busy;

    public LoginFlow(App app, IServiceProvider services)
    {
        _app = app;
        _authService = services.GetRequiredService<IAuthService>();
    }

    public void Draw()
    {
        _box = DialogRunner.DrawCenteredBox(_app, "Login to DAISI", 50, 14);

        DialogRunner.DrawLabel(_box, 0, "Email or Phone:");
        DialogRunner.DrawTextField(_box, 1, _emailBuffer.ToString(), _step == Step.Email && !_busy);

        if (_step >= Step.Code)
        {
            DialogRunner.DrawLabel(_box, 3, "Auth Code:");
            DialogRunner.DrawTextField(_box, 4, _codeBuffer.ToString(), _step == Step.Code && !_busy);
        }
        else
        {
            // Clear code area
            DialogRunner.DrawLabel(_box, 3, "");
            DialogRunner.DrawLabel(_box, 4, new string(' ', _box.InnerWidth));
        }

        // Status
        if (_statusText.Length > 0)
            DialogRunner.DrawStatus(_box, _statusText, _statusColor);

        // Button hints
        var hints = _step switch
        {
            Step.Email => " Enter:Send Code  Esc:Skip ",
            Step.Code => " Enter:Verify  Esc:Cancel ",
            _ => " Esc:Close "
        };
        DialogRunner.DrawButtonHints(_box, hints);

        // Cursor positioning
        if (!_busy)
        {
            AnsiConsole.ShowCursor();
            if (_step == Step.Email)
                AnsiConsole.MoveTo(_box.InnerTop + 1, _box.InnerLeft + _emailCursor);
            else if (_step == Step.Code)
                AnsiConsole.MoveTo(_box.InnerTop + 4, _box.InnerLeft + _codeCursor);
        }
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

        if (_step == Step.Email)
            HandleEmailInput(key);
        else if (_step == Step.Code)
            HandleCodeInput(key);
        else
            _app.CloseModal();
    }

    private void HandleEmailInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                _emailOrPhone = _emailBuffer.ToString().Trim();
                if (string.IsNullOrWhiteSpace(_emailOrPhone)) return;
                SendCode();
                break;
            case ConsoleKey.Backspace:
                if (_emailCursor > 0) { _emailBuffer.Remove(_emailCursor - 1, 1); _emailCursor--; }
                Draw();
                break;
            case ConsoleKey.Delete:
                if (_emailCursor < _emailBuffer.Length) { _emailBuffer.Remove(_emailCursor, 1); }
                Draw();
                break;
            case ConsoleKey.LeftArrow:
                if (_emailCursor > 0) _emailCursor--;
                Draw();
                break;
            case ConsoleKey.RightArrow:
                if (_emailCursor < _emailBuffer.Length) _emailCursor++;
                Draw();
                break;
            case ConsoleKey.Home:
                _emailCursor = 0;
                Draw();
                break;
            case ConsoleKey.End:
                _emailCursor = _emailBuffer.Length;
                Draw();
                break;
            default:
                if (key.KeyChar >= 32 && key.KeyChar < 127)
                {
                    _emailBuffer.Insert(_emailCursor, key.KeyChar);
                    _emailCursor++;
                    Draw();
                }
                break;
        }
    }

    private void HandleCodeInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                var code = _codeBuffer.ToString().Trim();
                if (string.IsNullOrWhiteSpace(code)) return;
                VerifyCode(code);
                break;
            case ConsoleKey.Backspace:
                if (_codeCursor > 0) { _codeBuffer.Remove(_codeCursor - 1, 1); _codeCursor--; }
                Draw();
                break;
            case ConsoleKey.Delete:
                if (_codeCursor < _codeBuffer.Length) { _codeBuffer.Remove(_codeCursor, 1); }
                Draw();
                break;
            case ConsoleKey.LeftArrow:
                if (_codeCursor > 0) _codeCursor--;
                Draw();
                break;
            case ConsoleKey.RightArrow:
                if (_codeCursor < _codeBuffer.Length) _codeCursor++;
                Draw();
                break;
            case ConsoleKey.Home:
                _codeCursor = 0;
                Draw();
                break;
            case ConsoleKey.End:
                _codeCursor = _codeBuffer.Length;
                Draw();
                break;
            default:
                if (key.KeyChar >= 32 && key.KeyChar < 127)
                {
                    _codeBuffer.Insert(_codeCursor, key.KeyChar);
                    _codeCursor++;
                    Draw();
                }
                break;
        }
    }

    private void SendCode()
    {
        _busy = true;
        SetStatus("Sending code...", ConsoleColor.Yellow);
        Draw();
        AnsiConsole.Flush();

        Task.Run(async () =>
        {
            try
            {
                var success = await _authService.SendAuthCodeAsync(_emailOrPhone);
                _app.Post(() =>
                {
                    _busy = false;
                    if (success)
                    {
                        SetStatus("Code sent! Enter it below.", ConsoleColor.Green);
                        _step = Step.Code;
                    }
                    else
                    {
                        SetStatus("Failed to send code.", ConsoleColor.Red);
                    }
                    Draw();
                    AnsiConsole.Flush();
                });
            }
            catch (Exception ex)
            {
                _app.Post(() =>
                {
                    _busy = false;
                    SetStatus($"Error: {ex.Message}", ConsoleColor.Red);
                    Draw();
                    AnsiConsole.Flush();
                });
            }
        });
    }

    private void VerifyCode(string code)
    {
        _busy = true;
        SetStatus("Verifying...", ConsoleColor.Yellow);
        Draw();
        AnsiConsole.Flush();

        Task.Run(async () =>
        {
            try
            {
                var state = await _authService.ValidateAuthCodeAsync(_emailOrPhone, code);
                _app.Post(() =>
                {
                    _busy = false;
                    SetStatus($"Welcome, {state.UserName}!", ConsoleColor.Green);
                    _step = Step.Done;
                    Draw();
                    AnsiConsole.Flush();

                    // Auto-close after a brief moment
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        _app.Post(() =>
                        {
                            AnsiConsole.HideCursor();
                            _app.CloseModal();
                        });
                    });
                });
            }
            catch (Exception ex)
            {
                _app.Post(() =>
                {
                    _busy = false;
                    SetStatus($"Error: {ex.Message}", ConsoleColor.Red);
                    Draw();
                    AnsiConsole.Flush();
                });
            }
        });
    }

    private void SetStatus(string text, ConsoleColor color)
    {
        _statusText = text;
        _statusColor = color;
    }
}
