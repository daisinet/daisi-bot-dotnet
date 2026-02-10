namespace DaisiBot.Shared.UI.Services;

public class BotNavigationState
{
    public Guid? CurrentBotId { get; private set; }

    public event Action? Changed;

    public void SelectBot(Guid id)
    {
        CurrentBotId = id;
        Changed?.Invoke();
    }

    public void Clear()
    {
        CurrentBotId = null;
        Changed?.Invoke();
    }
}
