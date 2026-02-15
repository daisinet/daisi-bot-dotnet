using Daisi.SDK.Models;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using DaisiBot.Tui.Commands;
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
    private bool _hostMode;
    private bool _localhostMode;

    private const int SidebarWidth = 24;

    public ScreenRouter? ScreenRouter { get; set; }

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
        _chatPanel.CommandDispatcher = new SlashCommandDispatcher(app, _services, "chat");

        _sidebar.ConversationSelected += OnConversationSelected;
        _sidebar.NewConversationRequested += OnNewConversation;
        _sidebar.DeleteConversationRequested += OnDeleteConversation;

        _authService.AuthStateChanged += (_, state) =>
            _app.Post(() =>
            {
                UpdateTitle(state);
                if (_app.ActiveScreen == this)
                {
                    DrawTitleBar();
                    AnsiConsole.Flush();
                }
            });

        UpdateFocus();
        LoadInitial();
    }

    private void LoadInitial()
    {
        Task.Run(async () =>
        {
            var settings = await _settingsService.GetSettingsAsync();
            _hostMode = settings.HostModeEnabled;
#if DEBUG
            _localhostMode = settings.LocalhostModeEnabled;
            if (_localhostMode)
            {
                DaisiStaticSettings.ApplyUserSettings("localhost", 5001, true);
                _authService.AppId = "app-debug";
            }
#endif

            var authState = await _authService.GetAuthStateAsync();
            _app.Post(() =>
            {
                UpdateTitle(authState);
                DrawTitleBar();
                AnsiConsole.Flush();
            });

            _sidebar.LoadConversations(onLoaded: () =>
            {
                if (_sidebar.HasItems)
                    _sidebar.SelectFirst();
            });

            if (!authState.IsAuthenticated)
            {
                _app.Post(() =>
                {
                    // Only show login modal if this screen is currently active
                    if (_app.ActiveScreen != this) return;
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

    public void Activate()
    {
        // Auto-select first conversation and focus command line
        if (_sidebar.HasItems)
        {
            _sidebar.SelectFirst();
            // OnConversationSelected handler will set focus to Chat
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
#if DEBUG
        var modeLabel = _localhostMode ? "F7:Localhost" : (_hostMode ? "F7:DaisiNet" : "F7:SelfHost");
#else
        var modeLabel = _hostMode ? "F7:DaisiNet" : "F7:SelfHost";
#endif
        var bar = $" F1:Bots  F2:Chats  F3:Model  F4:Settings  F5:Login  F6:Skills  {modeLabel}  F10:Quit ";
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
                ShowModelPicker();
                return;
            case ConsoleKey.F4:
                ShowSettings();
                return;
            case ConsoleKey.F5:
                ShowLogin();
                return;
            case ConsoleKey.F6:
                ShowSkillBrowser();
                return;
            case ConsoleKey.F7:
                ShowHostModeToggle();
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
        _chatPanel.CommandDispatcher!.CurrentConversation = conversation;
        _focus = FocusTarget.Chat;
        UpdateFocus();
        Task.Run(async () =>
        {
            var chatService = _services.GetRequiredService<IChatService>();
            await chatService.CloseSessionAsync();

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
            var chatService = _services.GetRequiredService<IChatService>();
            await chatService.CloseSessionAsync();

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
                _chatPanel.CommandDispatcher!.CurrentConversation = conversation;
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

    private void ShowHostModeToggle()
    {
#if DEBUG
        var currentIndex = _localhostMode ? 2 : (_hostMode ? 0 : 1);
        var dialog = new HostModeDialog(_app, currentIndex, selection =>
        {
            switch (selection)
            {
                case 0: // SelfHost
                    _hostMode = true;
                    _localhostMode = false;
                    Task.Run(async () =>
                    {
                        var settings = await _settingsService.GetSettingsAsync();
                        settings.HostModeEnabled = true;
                        settings.LocalhostModeEnabled = false;
                        await _settingsService.SaveSettingsAsync(settings);
                        DaisiStaticSettings.ApplyUserSettings(settings.OrcDomain, settings.OrcPort, settings.OrcUseSsl);
                        _authService.AppId = "app-260209122215-qakyd";
                        var chatService = _services.GetRequiredService<IChatService>();
                        await chatService.CloseSessionAsync();
                        _app.Post(() => { _chatPanel.ClearConversation(); Draw(); AnsiConsole.Flush(); });
                    });
                    break;

                case 1: // DaisiNet
                    _hostMode = false;
                    _localhostMode = false;
                    Task.Run(async () =>
                    {
                        var settings = await _settingsService.GetSettingsAsync();
                        settings.HostModeEnabled = false;
                        settings.LocalhostModeEnabled = false;
                        await _settingsService.SaveSettingsAsync(settings);
                        DaisiStaticSettings.ApplyUserSettings(settings.OrcDomain, settings.OrcPort, settings.OrcUseSsl);
                        _authService.AppId = "app-260209122215-qakyd";
                        var chatService = _services.GetRequiredService<IChatService>();
                        await chatService.CloseSessionAsync();
                        _app.Post(() => { _chatPanel.ClearConversation(); Draw(); AnsiConsole.Flush(); });
                    });
                    break;

                case 2: // Localhost
                    _hostMode = false;
                    _localhostMode = true;
                    Task.Run(async () =>
                    {
                        var settings = await _settingsService.GetSettingsAsync();
                        settings.LocalhostModeEnabled = true;
                        settings.HostModeEnabled = false;
                        await _settingsService.SaveSettingsAsync(settings);
                        DaisiStaticSettings.ApplyUserSettings("localhost", 5001, true);
                        _authService.AppId = "app-debug";
                        var chatService = _services.GetRequiredService<IChatService>();
                        await chatService.CloseSessionAsync();
                        _app.Post(() => { _chatPanel.ClearConversation(); Draw(); AnsiConsole.Flush(); });
                    });
                    break;
            }
        });
        _app.RunModal(dialog);
#else
        var message = _hostMode
            ? "Switch to DaisiNet? Your credits will be spent and charges may apply depending on your setup."
            : "Enable Self-Hosted mode? When your bots are idle, your system will process requests for others on the network.";

        var confirmDialog = new ConfirmDialog(_app, message, confirmed =>
        {
            if (confirmed)
            {
                _hostMode = !_hostMode;
                Task.Run(async () =>
                {
                    var settings = await _settingsService.GetSettingsAsync();
                    settings.HostModeEnabled = _hostMode;
                    await _settingsService.SaveSettingsAsync(settings);
                    var chatService = _services.GetRequiredService<IChatService>();
                    await chatService.CloseSessionAsync();
                    _app.Post(() =>
                    {
                        _chatPanel.ClearConversation();
                        Draw();
                        AnsiConsole.Flush();
                    });
                });
            }
        });
        _app.RunModal(confirmDialog);
#endif
    }
}
