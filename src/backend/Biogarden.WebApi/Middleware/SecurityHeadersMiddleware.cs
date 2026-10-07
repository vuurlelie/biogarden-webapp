namespace Biogarden.WebApi.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private static readonly string[] SensitivePaths =
        ["/api/auth", "/api/cart", "/api/orders", "/api/profile", "/api/admin", "/api/checkout-drafts", "/api/addresses"];

    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "frame-src https://www.google.com https://maps.google.com; " +
        "img-src 'self' data: blob:; " +
        "style-src 'self' 'unsafe-inline'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "connect-src 'self' http://localhost:5173 ws://localhost:5173";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            if (SensitivePaths.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase)))
            {
                headers.CacheControl = "no-store";
                headers.Pragma = "no-cache";
            }
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            headers.TryAdd(
                "Permissions-Policy",
                "camera=(), microphone=(), geolocation=(), payment=()");
            headers.TryAdd("Content-Security-Policy", ContentSecurityPolicy);

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
