using Biogarden.WebApi.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Biogarden.WebApi.Tests;

public sealed class SecurityHeadersTests
{
    [Theory]
    [InlineData("/api/addresses", true)]
    [InlineData("/api/addresses/42", true)]
    [InlineData("/api/products", false)]
    public async Task SuccessfulResponses_ProtectAddressDataWithoutChangingPublicCaching(string path, bool sensitive)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.Run(context => context.Response.WriteAsync("response"));
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        Assert.Equal(sensitive, response.Headers.CacheControl?.NoStore == true);
        Assert.Equal(sensitive, response.Headers.Pragma.Any(value => value.Name == "no-cache"));
    }
}
