namespace Biogarden.DataAccess.Entities;

public sealed class BrowserSession
{
    public string KeyHash { get; set; } = string.Empty;
    public byte[] ProtectedTicket { get; set; } = [];
    public DateTime ExpiresAtUtc { get; set; }
}
