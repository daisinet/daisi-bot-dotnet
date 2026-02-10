using DaisiBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DaisiBot.Tui;

public class ScreenRouter
{
    private readonly App _app;
    private readonly IScreen _chatScreen;
    private readonly IScreen _botScreen;
    private readonly ISettingsService _settingsService;

    public ScreenRouter(App app, IScreen chatScreen, IScreen botScreen)
    {
        _app = app;
        _chatScreen = chatScreen;
        _botScreen = botScreen;
        _settingsService = app.Services.GetRequiredService<ISettingsService>();
    }

    public IScreen GetScreen(string name) =>
        name == "chats" ? _chatScreen : _botScreen;

    public bool HandleGlobalKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.F1:
                SwitchTo(_botScreen, "bots");
                return true;
            case ConsoleKey.F2:
                SwitchTo(_chatScreen, "chats");
                return true;
            case ConsoleKey.F10:
                _app.Quit();
                return true;
        }
        return false;
    }

    private void SwitchTo(IScreen screen, string name)
    {
        _app.SetScreen(screen);
        Task.Run(async () =>
        {
            var settings = await _settingsService.GetSettingsAsync();
            settings.LastScreen = name;
            await _settingsService.SaveSettingsAsync(settings);
        });
    }
}
