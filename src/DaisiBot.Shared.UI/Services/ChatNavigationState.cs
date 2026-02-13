namespace DaisiBot.Shared.UI.Services;

public class ChatNavigationState : NavigationState<Guid>
{
    public Guid? CurrentConversationId => CurrentId;
    public void SelectConversation(Guid id) => Select(id);
}
