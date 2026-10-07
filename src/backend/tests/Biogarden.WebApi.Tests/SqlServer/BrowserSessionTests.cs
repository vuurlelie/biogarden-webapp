using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using Biogarden.DataAccess.Entities;
using Biogarden.WebApi.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class BrowserSessionTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task ActivityRenewsSession_PassiveReadsDoNot_AndIdleExpiryIsAnonymous()
    {
        var clock = new SessionClock();
        using var api = _app.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var client = api.CreateClient();
        await CreateUser();
        var login = await Login(client);
        var json = await login.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sessionId", json);
        var cookie = login.Headers.GetValues("Set-Cookie").Last(value => value.StartsWith("biogarden_session="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        clock.Advance(TimeSpan.FromMinutes(59));
        client.DefaultRequestHeaders.Add("X-Session-Activity", "1");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Session-Activity");
        clock.Advance(TimeSpan.FromMinutes(59));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
    }

    [SqlServerFact]
    public async Task LogoutRevokesCopiedCookie_AndOtherLoginStaysActive()
    {
        await CreateUser();
        using var first = _app.Factory.CreateClient();
        using var other = _app.Factory.CreateClient();
        var login = await Login(first);
        await Login(other);
        var cookie = login.Headers.GetValues("Set-Cookie").Last(x => x.StartsWith("biogarden_session=")).Split(';')[0];
        Assert.Equal(HttpStatusCode.NoContent, (await first.PostAsync("/api/auth/logout", null)).StatusCode);
        using var replay = _app.Factory.CreateClient(new() { HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.NoContent, (await replay.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/api/auth/session")).StatusCode);
    }

    [SqlServerFact]
    public async Task RepeatedLoginRotatesSessionAndRevokesPreviousCookie()
    {
        await CreateUser();
        using var client = _app.Factory.CreateClient();
        var first = await Login(client);
        var oldCookie = first.Headers.GetValues("Set-Cookie").Last(x => x.StartsWith("biogarden_session=")).Split(';')[0];
        var oldSession = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString();
        var second = await Login(client);
        Assert.NotEqual(oldSession, (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
        using var replay = _app.Factory.CreateClient(new() { HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", oldCookie);
        Assert.Equal(HttpStatusCode.NoContent, (await replay.GetAsync("/api/auth/session")).StatusCode);
    }

    [SqlServerFact]
    public async Task BrowserMutationsRejectMissingHeader_UntrustedOrigin_AndStaleIdentity()
    {
        await CreateUser();
        using var client = _app.Factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Checkout-Request");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", Credentials())).StatusCode);
        client.DefaultRequestHeaders.Add("X-Checkout-Request", "1");
        await Login(client);
        client.DefaultRequestHeaders.Remove("X-Checkout-Request");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Checkout-Request", "1");
        client.DefaultRequestHeaders.Add("Origin", "https://untrusted.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("X-Browser-Session", "old-session");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Browser-Session");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [SqlServerFact]
    public async Task AccountInvalidationRevokesCookieSession()
    {
        await CreateUser();
        using var client = _app.Factory.CreateClient();
        await Login(client);
        await using (var db = _app.OpenDatabase())
            await db.Users.ExecuteUpdateAsync(s => s.SetProperty(x => x.SecurityStamp, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [SqlServerFact]
    public async Task TicketRenewalCannotResurrectRevokedOrExpiredSession()
    {
        var clock = new SessionClock();
        using var api = _app.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        var store = api.Services.GetRequiredService<DatabaseTicketStore>();
        var properties = new AuthenticationProperties { ExpiresUtc = clock.GetUtcNow().AddMinutes(60) };
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("test", "value")], "test")), properties, "test");
        var key = await store.StoreAsync(ticket);
        Assert.NotNull(await store.RetrieveAsync(key));
        await store.RemoveAsync(key);
        properties.ExpiresUtc = clock.GetUtcNow().AddHours(2);
        await store.RenewAsync(key, ticket);
        Assert.Null(await store.RetrieveAsync(key));
        key = await store.StoreAsync(ticket);
        clock.Advance(TimeSpan.FromHours(3));
        properties.ExpiresUtc = clock.GetUtcNow().AddHours(1);
        await store.RenewAsync(key, ticket);
        Assert.Null(await store.RetrieveAsync(key));
    }

    private async Task CreateUser()
    {
        using var scope = _app.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await users.CreateAsync(new ApplicationUser { Id = Guid.NewGuid(), Email = "session@example.com", UserName = "session@example.com", EmailConfirmed = true }, "Password123!")).Succeeded);
    }
    private static object Credentials() => new { email = "session@example.com", password = "Password123!" };
    private static async Task<HttpResponseMessage> Login(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", Credentials());
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return response;
    }
    private sealed class SessionClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }
}
