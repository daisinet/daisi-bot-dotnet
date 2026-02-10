using DaisiBot.Core.Enums;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using DaisiBot.Tui.Commands;
using DaisiBot.Tui.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Screens;

public class BotMainScreen : IScreen
{
    private readonly App _app;
    private readonly IServiceProvider _services;
    private readonly IBotStore _botStore;
    private readonly IBotEngine _botEngine;
    private readonly BotSidebarPanel _sidebar;
    private readonly BotOutputPanel _outputPanel;
    private readonly SlashCommandDispatcher _commandDispatcher;

    private BotInstance? _currentBot;
    private string _titleText = "Daisi Bot - Bots";

    private const int SidebarWidth = 24;

    private enum FocusTarget { Sidebar, Output }
    private FocusTarget _focus = FocusTarget.Sidebar;

    public ScreenRouter? ScreenRouter { get; set; }

    public BotMainScreen(App app)
    {
        _app = app;
        _services = app.Services;
        _botStore = _services.GetRequiredService<IBotStore>();
        _botEngine = _services.GetRequiredService<IBotEngine>();

        _sidebar = new BotSidebarPanel(app, _botStore);
        _outputPanel = new BotOutputPanel(app, _services);

        _commandDispatcher = new SlashCommandDispatcher(app, _services, "bot");
        _commandDispatcher.OnBotDeleted = OnBotKilledViaCommand;
        _outputPanel.CommandDispatcher = _commandDispatcher;

        _sidebar.BotSelected += OnBotSelected;
        _sidebar.NewBotRequested += OnNewBot;
        _sidebar.DeleteBotRequested += OnDeleteBot;

        _botEngine.BotStatusChanged += OnBotStatusChanged;

        UpdateFocus();
        LoadInitial();
    }

    private void LoadInitial()
    {
        Task.Run(async () =>
        {
            var authService = _services.GetRequiredService<IAuthService>();
            var authState = await authService.GetAuthStateAsync();
            _app.Post(() =>
            {
                _titleText = authState.IsAuthenticated
                    ? $"Daisi Bot - Bots - Welcome, {authState.UserName}"
                    : "Daisi Bot - Bots - Not logged in";
            });
        });

        _sidebar.LoadBots(onLoaded: () =>
        {
            if (_sidebar.HasItems)
            {
                _sidebar.SelectFirst();
                // OnBotSelected will set focus to Output and load bot log
            }
        });
    }

    private void OnBotStatusChanged(object? sender, BotInstance bot)
    {
        _app.Post(() =>
        {
            _sidebar.UpdateFlashingBots(bot);
            _sidebar.LoadBots();

            if (_currentBot?.Id == bot.Id)
            {
                _currentBot = bot;
                _commandDispatcher.CurrentBot = bot;
                _outputPanel.UpdateStatus(bot);
            }

            // Audio notification for WaitingForInput
            if (bot.Status == BotStatus.WaitingForInput)
            {
                try { Console.Beep(); } catch { }

                if (_currentBot?.Id == bot.Id && bot.PendingQuestion is not null)
                {
                    _outputPanel.AppendLogEntry(new BotLogEntry
                    {
                        BotId = bot.Id,
                        Level = BotLogLevel.UserPrompt,
                        Message = bot.PendingQuestion
                    });
                }
            }

            AnsiConsole.Flush();
        });
    }

    public void Activate()
    {
        // Auto-select first bot and focus command line
        if (_sidebar.HasItems)
        {
            _sidebar.SelectFirst();
            // OnBotSelected handler will set focus to Output
        }
        else
        {
            _focus = FocusTarget.Sidebar;
            UpdateFocus();
        }
    }

    public void Draw()
    {
        var w = _app.Width;
        var h = _app.Height;

        _sidebar.Top = 1;
        _sidebar.Left = 0;
        _sidebar.Width = SidebarWidth;
        _sidebar.Height = h - 2;

        _outputPanel.Top = 1;
        _outputPanel.Left = SidebarWidth;
        _outputPanel.Width = w - SidebarWidth;
        _outputPanel.Height = h - 2;

        DrawTitleBar();
        _sidebar.Draw();
        _outputPanel.Draw();
        DrawStatusBar();
    }

    private void DrawTitleBar()
    {
        var w = _app.Width;
        AnsiConsole.SetReverse();
        AnsiConsole.WriteAt(0, 0, _titleText.PadRight(w));
        AnsiConsole.ResetStyle();
    }

    private void DrawStatusBar()
    {
        var w = _app.Width;
        var row = _app.Height - 1;
        var bar = " F1:Bots  F2:Chats  F3:Model  F4:Settings  F5:Login  F6:Skills  F10:Quit ";
        AnsiConsole.SetReverse();
        var padded = bar.Length >= w ? bar[..w] : bar + new string(' ', w - bar.Length);
        AnsiConsole.WriteAt(row, 0, padded);
        AnsiConsole.ResetStyle();
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        // Screen router handles F1/F2/F10
        if (ScreenRouter?.HandleGlobalKey(key) == true)
            return;

        // Screen-local F-keys
        switch (key.Key)
        {
            case ConsoleKey.F3:
                _app.RunModal(new ModelPickerFlow(_app, _services));
                return;
            case ConsoleKey.F4:
                _app.RunModal(new SettingsFlow(_app, _services));
                return;
            case ConsoleKey.F5:
                _app.RunModal(new LoginFlow(_app, _services));
                return;
            case ConsoleKey.F6:
                _app.RunModal(new SkillBrowserFlow(_app, _services));
                return;
            case ConsoleKey.Tab:
                ToggleFocus();
                Draw();
                return;
        }

        if (_focus == FocusTarget.Sidebar)
            _sidebar.HandleKey(key);
        else
            _outputPanel.HandleKey(key);
    }

    private void ToggleFocus()
    {
        _focus = _focus == FocusTarget.Sidebar ? FocusTarget.Output : FocusTarget.Sidebar;
        UpdateFocus();
    }

    private void UpdateFocus()
    {
        _sidebar.HasFocus = _focus == FocusTarget.Sidebar;
        _outputPanel.HasFocus = _focus == FocusTarget.Output;
    }

    private void OnBotSelected(BotInstance bot)
    {
        _currentBot = bot;
        _commandDispatcher.CurrentBot = bot;
        _focus = FocusTarget.Output;
        UpdateFocus();
        _outputPanel.SetBot(bot);
        Draw();
        AnsiConsole.Flush();
    }

    private void OnNewBot()
    {
        var flow = new BotCreationFlow(_app, _services, bot =>
        {
            _sidebar.LoadBots();
            _currentBot = bot;
            _commandDispatcher.CurrentBot = bot;
            _outputPanel.SetBot(bot);
            _focus = FocusTarget.Output;
            UpdateFocus();
            Draw();
            AnsiConsole.Flush();
        });
        _app.RunModal(flow);
    }

    private void OnDeleteBot()
    {
        if (_currentBot is null) return;
        var confirmDialog = new ConfirmDialog(_app, "Delete this bot?", confirmed =>
        {
            if (confirmed)
            {
                var idToDelete = _currentBot.Id;
                _currentBot = null;
                _commandDispatcher.CurrentBot = null;
                Task.Run(async () =>
                {
                    var engine = _services.GetRequiredService<IBotEngine>();
                    if (engine.IsRunning(idToDelete))
                        await engine.StopBotAsync(idToDelete);
                    await _botStore.DeleteAsync(idToDelete);
                    _app.Post(() =>
                    {
                        _outputPanel.ClearBot();
                        _sidebar.LoadBots(onLoaded: () =>
                        {
                            if (_sidebar.HasItems)
                            {
                                _sidebar.SelectFirst();
                            }
                            else
                            {
                                _focus = FocusTarget.Sidebar;
                                UpdateFocus();
                                Draw();
                                AnsiConsole.Flush();
                            }
                        });
                    });
                });
            }
        });
        _app.RunModal(confirmDialog);
    }

    private void OnBotKilledViaCommand()
    {
        _currentBot = null;
        _commandDispatcher.CurrentBot = null;
        _outputPanel.ClearBot();
        _sidebar.LoadBots(onLoaded: () =>
        {
            if (_sidebar.HasItems)
            {
                _sidebar.SelectFirst();
            }
            else
            {
                _focus = FocusTarget.Sidebar;
                UpdateFocus();
                Draw();
                AnsiConsole.Flush();
            }
        });
    }
}
