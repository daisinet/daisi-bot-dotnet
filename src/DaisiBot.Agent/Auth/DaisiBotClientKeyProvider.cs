using DaisiBot.Core.Models;
using Daisi.SDK.Interfaces.Authentication;

namespace DaisiBot.Agent.Auth;

public class DaisiBotClientKeyProvider : IClientKeyProvider
{
    private AuthState _authState = new();

    public void UpdateAuthState(AuthState state) => _authState = state;

    public string GetClientKey() => _authState.ClientKey;
}
