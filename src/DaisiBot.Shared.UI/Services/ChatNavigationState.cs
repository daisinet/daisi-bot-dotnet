namespace DaisiBot.Shared.UI.Services;

public class ChatNavigationState
{
    public Guid? CurrentConversationId { get; private set; }

    public event Action? Changed;

    public void SelectConversation(Guid id)
    {
        CurrentConversationId = id;
        Changed?.Invoke();
    }

    public void Clear()
    {
        CurrentConversationId = null;
        Changed?.Invoke();
    }
}
