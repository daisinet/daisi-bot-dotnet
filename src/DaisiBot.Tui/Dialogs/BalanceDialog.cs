using Daisi.Protos.V1;
using Daisi.SDK.Clients.V1.Orc;
using DaisiBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Dialogs;

public class BalanceDialog : IModal
{
    private readonly App _app;
    private readonly IServiceProvider _services;
    private DialogRunner.BoxBounds? _box;

    private enum State { Loading, Loaded, Error }
    private State _state = State.Loading;
    private bool _fetchStarted;

    private string _accountName = "";
    private string _email = "";
    private string _accountId = "";
    private long _balance;
    private long _totalEarned;
    private long _totalSpent;
    private string _errorMessage = "";

    public BalanceDialog(App app, IServiceProvider services)
    {
        _app = app;
        _services = services;
    }

    public void Draw()
    {
        if (!_fetchStarted)
        {
            _fetchStarted = true;
            Task.Run(FetchBalanceAsync);
        }

        const int boxWidth = 50;

        switch (_state)
        {
            case State.Loading:
                DrawLoading(boxWidth);
                break;
            case State.Loaded:
                DrawLoaded(boxWidth);
                break;
            case State.Error:
                DrawError(boxWidth);
                break;
        }
    }

    private void DrawLoading(int boxWidth)
    {
        _box = DialogRunner.DrawCenteredBox(_app, "Balance", boxWidth, 5);
        DialogRunner.DrawLabel(_box, 1, "Loading...");
        DialogRunner.DrawButtonHints(_box, " Esc:Close ");
    }

    private void DrawLoaded(int boxWidth)
    {
        _box = DialogRunner.DrawCenteredBox(_app, "Balance", boxWidth, 12);

        DialogRunner.DrawLabel(_box, 0, "Account Information");

        var row = 2;
        DrawField(row++, "Account:", _accountName);
        DrawField(row++, "Email:", _email);
        DrawField(row++, "Account ID:", _accountId);

        row++;
        DialogRunner.DrawLabel(_box, row++, "Credit Balance");
        DrawField(row++, "Balance:", _balance.ToString("N0"));
        DrawField(row++, "Total Earned:", _totalEarned.ToString("N0"));
        DrawField(row, "Total Spent:", _totalSpent.ToString("N0"));

        DialogRunner.DrawButtonHints(_box, " Esc:Close ");
    }

    private void DrawError(int boxWidth)
    {
        var lines = WordWrap(_errorMessage, boxWidth - 4);
        var height = lines.Count + 4;
        _box = DialogRunner.DrawCenteredBox(_app, "Balance", boxWidth, height);

        AnsiConsole.SetForeground(ConsoleColor.Red);
        for (var i = 0; i < lines.Count; i++)
            DialogRunner.DrawLabel(_box, 1 + i, lines[i]);
        AnsiConsole.ResetStyle();

        DialogRunner.DrawButtonHints(_box, " Esc:Close ");
    }

    private void DrawField(int row, string label, string value)
    {
        if (_box is null) return;
        var text = $"{label,-14} {value}";
        if (text.Length > _box.InnerWidth)
            text = text[.._box.InnerWidth];
        AnsiConsole.WriteAt(_box.InnerTop + row, _box.InnerLeft, text);
    }

    private async Task FetchBalanceAsync()
    {
        try
        {
            var authService = _services.GetRequiredService<IAuthService>();
            var authState = await authService.GetAuthStateAsync();

            if (!authState.IsAuthenticated)
            {
                _errorMessage = "Not authenticated. Use /login first.";
                _state = State.Error;
                _app.Post(Draw);
                return;
            }

            _email = authState.UserEmail;
            _accountName = authState.AccountName;
            _accountId = authState.AccountId;

            var creditFactory = _services.GetRequiredService<CreditClientFactory>();
            var creditClient = creditFactory.Create();
            var creditResponse = await creditClient.GetCreditAccountAsync(
                new GetCreditAccountRequest { AccountId = authState.AccountId });

            if (creditResponse?.Account is not null)
            {
                _balance = creditResponse.Account.Balance;
                _totalEarned = creditResponse.Account.TotalEarned;
                _totalSpent = creditResponse.Account.TotalSpent;
            }

            _state = State.Loaded;
            _app.Post(Draw);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to fetch balance: {ex.Message}";
            _state = State.Error;
            _app.Post(Draw);
        }
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
            _app.CloseModal();
    }

    private static List<string> WordWrap(string text, int maxWidth)
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        var current = "";

        foreach (var word in words)
        {
            if (current.Length == 0)
                current = word;
            else if (current.Length + 1 + word.Length <= maxWidth)
                current += " " + word;
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
