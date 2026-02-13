namespace DaisiBot.Shared.UI.Services;

public class BotNavigationState : NavigationState<Guid>
{
    public Guid? CurrentBotId => CurrentId;
    public void SelectBot(Guid id) => Select(id);
}
