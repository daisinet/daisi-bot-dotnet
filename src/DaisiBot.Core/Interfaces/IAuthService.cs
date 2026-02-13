using DaisiBot.Core.Models;

namespace DaisiBot.Core.Interfaces;

public interface IAuthService
{
    string AppId { get; set; }
    Task<bool> SendAuthCodeAsync(string emailOrPhone);
    Task<AuthState> ValidateAuthCodeAsync(string emailOrPhone, string code);
    Task LogoutAsync();
    Task<AuthState> GetAuthStateAsync();
    event EventHandler<AuthState>? AuthStateChanged;
}
