namespace DaisiBot.Core.Models;

public class AuthState
{
    public int Id { get; set; } = 1;
    public string ClientKey { get; set; } = string.Empty;
    public DateTime? KeyExpiration { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(ClientKey) &&
        (!KeyExpiration.HasValue || KeyExpiration.Value > DateTime.UtcNow);
}
