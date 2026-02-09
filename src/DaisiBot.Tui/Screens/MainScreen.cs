using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using DaisiBot.Tui.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui.Screens;

/// <summary>
/// Main screen with three zones: title bar, sidebar + chat panel, status bar.
/// Routes keyboard input and manages focus.
/// </summary>
public class MainScreen : IScreen
{
    private readonly App _app;
    private readonly IServiceProvider _services;
    private readonly IConversationStore _conversationStore;
    private readonly IAuthService _authService;
    private readonly ISettingsService _settingsService;
    private readonly SidebarPanel _sidebar;
    private readonly ChatPanel _chatPanel;

    private string _titleText = "Daisi Bot - Not logged in";
    private Conversation? _currentConversation;

    private const int SidebarWidth = 24;

    private enum FocusTarget { Sidebar, Chat }
    private FocusTarget _focus = FocusTarget.Sidebar;

    public MainScreen(App app)
    {
        _app = app;
        _services = app.Services;
        _conversationStore = _services.GetRequiredService<IConversationStore>();
        _authService = _services.GetRequiredService<IAuthService>();
        _settingsService = _services.GetRequiredService<ISettingsService>();

        _sidebar = new SidebarPanel(app, _conversationStore);
        _chatPanel = new ChatPanel(app, _services);

        _sidebar.ConversationSelected += OnConversationSelected;
        _sidebar.NewConversationRequested += OnNewConversation;
        _sidebar.DeleteConversationRequested += OnDeleteConversation;

        _authService.AuthStateChanged += (_, state) =>
            _app.Post(() =>
            {
                UpdateTitle(state);
                DrawTitleBar();
                AnsiConsole.Flush();
            });

        UpdateFocus();
        LoadInitial();
    }

    private void LoadInitial()
    {
        Task.Run(async () =>
        {
            var authState = await _authService.GetAuthStateAsync();
            _app.Post(() =>
            {
                UpdateTitle(authState);
                DrawTitleBar();
                AnsiConsole.Flush();
            });

            _sidebar.LoadConversations();

            if (!authState.IsAuthenticated)
            {
                _app.Post(() =>
                {
                    var loginFlow = new LoginFlow(_app, _services);
                    _app.RunModal(loginFlow);
                });
            }
        });
    }

    private void UpdateTitle(AuthState state)
    {
        _titleText = state.IsAuthenticated
            ? $"Daisi Bot - Welcome, {state.UserName}"
            : "Daisi Bot - Not logged in";
    }

    public void Draw()
    {
        var w = _app.Width;
        var h = _app.Height;

        // Layout
        _sidebar.Top = 1;
        _sidebar.Left = 0;
        _sidebar.Width = SidebarWidth;
        _sidebar.Height = h - 2;

        _chatPanel.Top = 1;
        _chatPanel.Left = SidebarWidth;
        _chatPanel.Width = w - SidebarWidth;
        _chatPanel.Height = h - 2;

        DrawTitleBar();
        _sidebar.Draw();
        _chatPanel.Draw();
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
        var bar = " F2:Model  F3:Settings  F4:Login  F5:Skills  F10:Quit ";
        AnsiConsole.SetReverse();
        var padded = bar.Length >= w ? bar[..w] : bar + new string(' ', w - bar.Length);
        AnsiConsole.WriteAt(row, 0, padded);
        AnsiConsole.ResetStyle();
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        // Global F-keys first
        switch (key.Key)
        {
            case ConsoleKey.F2:
                ShowModelPicker();
                return;
            case ConsoleKey.F3:
                ShowSettings();
                return;
            case ConsoleKey.F4:
                ShowLogin();
                return;
            case ConsoleKey.F5:
                ShowSkillBrowser();
                return;
            case ConsoleKey.F10:
                _app.Quit();
                return;
            case ConsoleKey.Tab:
                ToggleFocus();
                Draw();
                return;
        }

        // Route to focused panel
        if (_focus == FocusTarget.Sidebar)
        {
            if (!_sidebar.HandleKey(key))
            {
                // Unhandled: maybe switch focus if it's a typing character
            }
        }
        else
        {
            if (!_chatPanel.HandleKey(key))
            {
                // Unhandled
            }
        }
    }

    private void ToggleFocus()
    {
        _focus = _focus == FocusTarget.Sidebar ? FocusTarget.Chat : FocusTarget.Sidebar;
        UpdateFocus();
    }

    private void UpdateFocus()
    {
        _sidebar.HasFocus = _focus == FocusTarget.Sidebar;
        _chatPanel.HasFocus = _focus == FocusTarget.Chat;
    }

    private void OnConversationSelected(Conversation conversation)
    {
        _currentConversation = conversation;
        Task.Run(async () =>
        {
            var full = await _conversationStore.GetAsync(conversation.Id);
            if (full is not null)
            {
                _app.Post(() =>
                {
                    _chatPanel.SetConversation(full);
                    AnsiConsole.Flush();
                });
            }
        });
    }

    private void OnNewConversation()
    {
        Task.Run(async () =>
        {
            var settings = await _settingsService.GetSettingsAsync();
            var conversation = new Conversation
            {
                ModelName = settings.DefaultModelName,
                ThinkLevel = settings.DefaultThinkLevel,
                SystemPrompt = settings.SystemPrompt
            };
            await _conversationStore.CreateAsync(conversation);
            _app.Post(() =>
            {
                _sidebar.LoadConversations();
                _chatPanel.SetConversation(conversation);
                _focus = FocusTarget.Chat;
                UpdateFocus();
                Draw();
                AnsiConsole.Flush();
            });
        });
    }

    private void OnDeleteConversation()
    {
        if (_currentConversation is null) return;
        var confirmDialog = new ConfirmDialog(_app, "Delete this conversation?", confirmed =>
        {
            if (confirmed)
            {
                var idToDelete = _currentConversation.Id;
                _currentConversation = null;
                Task.Run(async () =>
                {
                    await _conversationStore.DeleteAsync(idToDelete);
                    _app.Post(() =>
                    {
                        _sidebar.LoadConversations();
                        _chatPanel.ClearConversation();
                        Draw();
                        AnsiConsole.Flush();
                    });
                });
            }
        });
        _app.RunModal(confirmDialog);
    }

    private void ShowLogin()
    {
        var flow = new LoginFlow(_app, _services);
        _app.RunModal(flow);
    }

    private void ShowModelPicker()
    {
        var flow = new ModelPickerFlow(_app, _services);
        _app.RunModal(flow);
    }

    private void ShowSettings()
    {
        var flow = new SettingsFlow(_app, _services);
        _app.RunModal(flow);
    }

    private void ShowSkillBrowser()
    {
        var flow = new SkillBrowserFlow(_app, _services);
        _app.RunModal(flow);
    }
}
