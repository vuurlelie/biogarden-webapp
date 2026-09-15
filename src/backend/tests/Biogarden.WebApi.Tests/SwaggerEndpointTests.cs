using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace Biogarden.WebApi.Tests;

public sealed class SwaggerEndpointTests
{
    [Fact]
    public async Task DevelopmentDocument_DescribesCheckoutRequestAndResponse()
    {
        using var factory = new BiogardenWebApiFactory();
        using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var checkout = document.RootElement.GetProperty("paths").GetProperty("/api/Orders").GetProperty("post");
        Assert.True(checkout.TryGetProperty("requestBody", out _));
        Assert.True(checkout.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(document.RootElement.GetProperty("components").GetProperty("schemas")
            .TryGetProperty("PlaceOrderDto", out _));
    }
}
