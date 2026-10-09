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
        var databaseContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        var expired = databaseContext.Set<BrowserSession>().Where(browserSession => browserSession.ExpiresAtUtc <= now)
            .OrderBy(browserSession => browserSession.ExpiresAtUtc).Take(100).Select(browserSession => browserSession.KeyHash);
        await databaseContext.Set<BrowserSession>().Where(browserSession => expired.Contains(browserSession.KeyHash)).ExecuteDeleteAsync();
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        databaseContext.Add(new BrowserSession
        {
            KeyHash = Hash(key),
            ProtectedTicket = _protector.Protect(TicketSerializer.Default.Serialize(ticket)),
            ExpiresAtUtc = ticket.Properties.ExpiresUtc!.Value.UtcDateTime
        });
        await databaseContext.SaveChangesAsync();
        return key;
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        using var scope = scopes.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = Hash(key);
        var now = clock.GetUtcNow().UtcDateTime;
        var stored = await databaseContext.Set<BrowserSession>().AsNoTracking().SingleOrDefaultAsync(browserSession => browserSession.KeyHash == hash && browserSession.ExpiresAtUtc > now);
        if (stored is null)
            return null;
        try { return TicketSerializer.Default.Deserialize(_protector.Unprotect(stored.ProtectedTicket)); }
        catch (CryptographicException) { await RemoveAsync(key); return null; }
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        using var scope = scopes.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = Hash(key);
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = ticket.Properties.ExpiresUtc!.Value.UtcDateTime;
        var payload = _protector.Protect(TicketSerializer.Default.Serialize(ticket));
        // Never resurrect a logged-out/expired session or shorten a newer concurrent renewal.
        await databaseContext.Set<BrowserSession>().Where(browserSession => browserSession.KeyHash == hash && browserSession.ExpiresAtUtc > now && browserSession.ExpiresAtUtc < expires)
            .ExecuteUpdateAsync(updateSettersBuilder => updateSettersBuilder.SetProperty(browserSession => browserSession.ProtectedTicket, payload).SetProperty(browserSession => browserSession.ExpiresAtUtc, expires));
    }

    public async Task RemoveAsync(string key)
    {
        using var scope = scopes.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = Hash(key);
        await databaseContext.Set<BrowserSession>().Where(browserSession => browserSession.KeyHash == hash).ExecuteDeleteAsync();
    }
}
