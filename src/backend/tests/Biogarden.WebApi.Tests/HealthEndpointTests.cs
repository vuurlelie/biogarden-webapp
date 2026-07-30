namespace Biogarden.WebApi.Tests;

public sealed class HealthEndpointTests : IClassFixture<BiogardenWebApiFactory>
{
    private readonly BiogardenWebApiFactory _factory;

    public HealthEndpointTests(BiogardenWebApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LiveHealthEndpoint_ReturnsHealthy()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        response.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LiveHealthEndpoint_ReturnsCorrelationIdHeader()
    {
        const string correlationId = "health-check-test";
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", correlationId);

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.Equal(correlationId, Assert.Single(values));
    }

    [Fact]
    public async Task LiveHealthEndpoint_ReturnsSecurityHeaders()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        response.EnsureSuccessStatusCode();
        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeader(response, "X-Frame-Options", "DENY");
        AssertHeader(response, "Referrer-Policy", "strict-origin-when-cross-origin");
        AssertHeader(
            response,
            "Permissions-Policy",
            "camera=(), microphone=(), geolocation=(), payment=()");
        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));
        var contentSecurityPolicy = Assert.Single(values);
        Assert.Contains("frame-ancestors 'none'", contentSecurityPolicy);
        Assert.Contains("frame-src https://www.google.com https://maps.google.com", contentSecurityPolicy);
        Assert.Contains("img-src 'self' data: blob:", contentSecurityPolicy);
    }

    private static void AssertHeader(
        HttpResponseMessage response,
        string headerName,
        string expectedValue)
    {
        Assert.True(response.Headers.TryGetValues(headerName, out var values));
        Assert.Equal(expectedValue, Assert.Single(values));
    }
}
