using Daisi.Protos.V1;
using Daisi.SDK.Clients.V1.Orc;
using DaisiBot.Core.Interfaces;
using DaisiBot.Core.Models;
using DaisiBot.Data.Stores;

namespace DaisiBot.Agent.Auth;

public class DaisiBotAuthService : IAuthService
{
    private readonly AuthClientFactory _authClientFactory;
    private readonly DaisiBotClientKeyProvider _keyProvider;
    private readonly SqliteAuthStateStore _authStore;
    private AuthState _currentState = new();

    public string AppId { get; set; } = "app-260209122215-qakyd";

    public event EventHandler<AuthState>? AuthStateChanged;

    public DaisiBotAuthService(
        AuthClientFactory authClientFactory,
        DaisiBotClientKeyProvider keyProvider,
        SqliteAuthStateStore authStore)
    {
        _authClientFactory = authClientFactory;
        _keyProvider = keyProvider;
        _authStore = authStore;
    }

    public async Task InitializeAsync()
    {
        _currentState = await _authStore.LoadAsync();
        if (_currentState.IsAuthenticated)
        {
            _keyProvider.UpdateAuthState(_currentState);
        }
    }

    public async Task<bool> SendAuthCodeAsync(string emailOrPhone)
    {
        var client = _authClientFactory.Create();
        var response = await client.SendAuthCodeAsync(new SendAuthCodeRequest
        {
            EmailOrPhone = emailOrPhone
        });
        return response.Success;
    }

    public async Task<AuthState> ValidateAuthCodeAsync(string emailOrPhone, string code)
    {
        var client = _authClientFactory.Create();
        var request = new ValidateAuthCodeRequest
        {
            EmailOrPhone = emailOrPhone,
            AuthCode = code,
            AppId = AppId
        };

        var response = await client.ValidateAuthCodeAsync(request);

        if (!response.Success)
        {
            throw new InvalidOperationException(response.ErrorMessage);
        }

        _currentState = new AuthState
        {
            ClientKey = response.ClientKey,
            KeyExpiration = response.KeyExpiration?.ToDateTime(),
            UserName = response.UserName,
            AccountName = response.AccountName,
            AccountId = response.AccountId,
            UserEmail = emailOrPhone
        };

        _keyProvider.UpdateAuthState(_currentState);
        await _authStore.SaveAsync(_currentState);
        AuthStateChanged?.Invoke(this, _currentState);

        return _currentState;
    }

    public async Task LogoutAsync()
    {
        _currentState = new AuthState();
        _keyProvider.UpdateAuthState(_currentState);
        await _authStore.ClearAsync();
        AuthStateChanged?.Invoke(this, _currentState);
    }

    public Task<AuthState> GetAuthStateAsync() => Task.FromResult(_currentState);
}
