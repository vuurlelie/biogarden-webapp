using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Biogarden.WebApi.Tests;

public sealed class ValidationResponseTests : IClassFixture<BiogardenWebApiFactory>
{
    private readonly HttpClient _client;
    public ValidationResponseTests(BiogardenWebApiFactory factory) => _client = factory.CreateClient();

    [Theory]
    [InlineData("/api/cart/items", "{\"productId\":0,\"quantity\":-1}", "quantity")]
    [InlineData("/api/orders", "{\"fulfillmentMethod\":999}", "fulfillmentMethod")]
    [InlineData("/api/auth/login", "{\"email\":null,\"password\":null}", "email")]
    public async Task InvalidRequest_IsRejectedBeforeDatabaseAccess(string path, string json, string field)
    {
        using var response = await _client.PostAsync(path, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(body.GetProperty("errors").GetProperty(field).EnumerateArray());
    }

    [Fact]
    public async Task InvalidEmail_ReturnsFieldErrorsWithCorrelation()
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.NotEmpty(body.GetProperty("errors").GetProperty("email").EnumerateArray());
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), body.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task MalformedJson_ReturnsValidationProblemInsteadOfServerError()
    {
        using var response = await _client.PostAsync("/api/auth/forgot-password",
            new StringContent("{", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(body.GetProperty("errors").EnumerateObject());
        Assert.True(body.TryGetProperty("traceId", out _));
    }
}
