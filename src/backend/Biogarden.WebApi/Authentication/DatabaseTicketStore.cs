using System.Security.Cryptography;
using System.Text;
using Biogarden.DataAccess;
using Biogarden.DataAccess.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.WebApi.Authentication;

public sealed class DatabaseTicketStore(IServiceScopeFactory scopes, IDataProtectionProvider protection, TimeProvider clock) : ITicketStore
{
    private readonly IDataProtector _protector = protection.CreateProtector("Biogarden.BrowserSessions.v1");
    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        var expired = db.Set<BrowserSession>().Where(x => x.ExpiresAtUtc <= now)
            .OrderBy(x => x.ExpiresAtUtc).Take(100).Select(x => x.KeyHash);
        await db.Set<BrowserSession>().Where(x => expired.Contains(x.KeyHash)).ExecuteDeleteAsync();
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.Add(new BrowserSession
        {
            KeyHash = Hash(key),
            ProtectedTicket = _protector.Protect(TicketSerializer.Default.Serialize(ticket)),
            ExpiresAtUtc = ticket.Properties.ExpiresUtc!.Value.UtcDateTime
        });
        await db.SaveChangesAsync();
        return key;
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = Hash(key);
        var now = clock.GetUtcNow().UtcDateTime;
        var stored = await db.Set<BrowserSession>().AsNoTracking().SingleOrDefaultAsync(x => x.KeyHash == hash && x.ExpiresAtUtc > now);
        if (stored is null) return null;
        try { return TicketSerializer.Default.Deserialize(_protector.Unprotect(stored.ProtectedTicket)); }
        catch (CryptographicException) { await RemoveAsync(key); return null; }
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = Hash(key);
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = ticket.Properties.ExpiresUtc!.Value.UtcDateTime;
        var payload = _protector.Protect(TicketSerializer.Default.Serialize(ticket));
        // Never resurrect a logged-out/expired session or shorten a newer concurrent renewal.
        await db.Set<BrowserSession>().Where(x => x.KeyHash == hash && x.ExpiresAtUtc > now && x.ExpiresAtUtc < expires)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ProtectedTicket, payload).SetProperty(x => x.ExpiresAtUtc, expires));
    }

    public async Task RemoveAsync(string key)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = Hash(key);
        await db.Set<BrowserSession>().Where(x => x.KeyHash == hash).ExecuteDeleteAsync();
    }
}
