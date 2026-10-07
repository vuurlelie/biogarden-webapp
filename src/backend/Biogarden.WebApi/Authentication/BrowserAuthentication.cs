using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.DataProtection;

namespace Biogarden.WebApi.Authentication;

public static class BrowserAuthentication
{
    public const string Scheme = "BrowserSession";
    public const string SessionClaim = "browser_session_id";
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromMinutes(60);

    public static IServiceCollection AddBrowserSessions(this IServiceCollection services, IConfiguration configuration)
    {
        var keyPath = configuration["BrowserSessions:DataProtectionKeyPath"]
            ?? Path.Combine(AppContext.BaseDirectory, ".private", "data-protection-keys");
        var dataProtection = services.AddDataProtection().SetApplicationName("Biogarden.WebApi")
            .PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        if (OperatingSystem.IsWindows()) dataProtection.ProtectKeysWithDpapi();
        services.AddSingleton<DatabaseTicketStore>();
        services.AddAuthentication().AddCookie(Scheme);
        services.AddOptions<CookieAuthenticationOptions>(Scheme)
            .Configure<DatabaseTicketStore, IHostEnvironment, TimeProvider>((options, store, environment, clock) =>
            {
                options.SessionStore = store;
                options.TimeProvider = clock;
                options.Cookie.Name = environment.IsDevelopment() || environment.IsEnvironment("Testing")
                    ? "biogarden_session" : "__Host-biogarden_session";
                options.Cookie.HttpOnly = true;
                options.Cookie.Path = "/";
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = environment.IsDevelopment() || environment.IsEnvironment("Testing")
                    ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = IdleLifetime;
                options.SlidingExpiration = false;
                options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
                options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
                options.Events.OnValidatePrincipal = async context =>
                {
                    if (context.Principal is null || !await SessionAuthentication.IsCurrentAsync(context.Principal,
                        context.HttpContext.RequestServices, context.HttpContext.RequestAborted))
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(Scheme);
                        return;
                    }
                    var request = context.Request;
                    if (request.Headers["X-Session-Activity"] != "1") return;
                    var origin = request.Headers.Origin;
                    if (origin.Count > 0)
                    {
                        var provider = context.HttpContext.RequestServices.GetRequiredService<ICorsPolicyProvider>();
                        var policy = await provider.GetPolicyAsync(context.HttpContext, "FrontendDev");
                        if (origin.Count != 1 || policy is null || !policy.IsOriginAllowed(origin.ToString())) return;
                    }
                    context.Properties.IssuedUtc = clock.GetUtcNow();
                    context.Properties.ExpiresUtc = clock.GetUtcNow().Add(IdleLifetime);
                    context.ShouldRenew = true;
                };
            });
        return services;
    }

    public static Biogarden.WebApi.Contracts.BrowserSessionResponse Describe(ClaimsPrincipal user, AuthenticationProperties properties) => new(
        user.FindFirstValue(ClaimTypes.NameIdentifier)!, user.FindFirstValue(ClaimTypes.Email)!,
        user.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray(), user.FindFirstValue(SessionClaim)!, properties.ExpiresUtc);
}
