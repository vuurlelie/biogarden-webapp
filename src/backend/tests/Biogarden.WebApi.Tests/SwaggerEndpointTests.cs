using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace Biogarden.WebApi.Tests;

public sealed class SwaggerEndpointTests
{
    [Theory]
    [InlineData("Production", "/swagger/index.html")]
    [InlineData("Production", "/swagger/v1/swagger.json")]
    [InlineData("Staging", "/swagger/index.html")]
    [InlineData("Staging", "/swagger/v1/swagger.json")]
    [InlineData("Testing", "/swagger/index.html")]
    [InlineData("Testing", "/swagger/v1/swagger.json")]
    public async Task NonDevelopmentDoesNotExposeDocumentation(string environment, string path)
    {
        using var factory = new BiogardenWebApiFactory();
        using var hosted = factory.WithWebHostBuilder(webHostBuilder => webHostBuilder.UseEnvironment(environment));
        using var client = hosted.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task DevelopmentDocument_DescribesCheckoutRequestAndResponse()
    {
        using var factory = new BiogardenWebApiFactory();
        using var development = factory.WithWebHostBuilder(webHostBuilder => webHostBuilder.UseEnvironment("Development"));
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Biogarden API", document.RootElement.GetProperty("info").GetProperty("title").GetString());
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var sessionProperties = schemas.GetProperty("BrowserSessionResponse").GetProperty("properties");
        Assert.True(sessionProperties.TryGetProperty("sessionId", out _));
        Assert.False(sessionProperties.TryGetProperty("token", out _));
        var checkout = document.RootElement.GetProperty("paths").GetProperty("/api/Orders").GetProperty("post");
        Assert.True(checkout.TryGetProperty("requestBody", out _));
        Assert.True(checkout.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(document.RootElement.GetProperty("components").GetProperty("schemas")
            .TryGetProperty("PlaceOrderDto", out _));
    }
}
