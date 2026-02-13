namespace DaisiBot.Shared.UI.Services;

public class NavigationState<T> where T : struct
{
    public T? CurrentId { get; private set; }

    public event Action? Changed;

    public void Select(T id)
    {
        CurrentId = id;
        Changed?.Invoke();
    }

    public void Clear()
    {
        CurrentId = null;
        Changed?.Invoke();
    }
}
