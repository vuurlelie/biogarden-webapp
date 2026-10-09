using Microsoft.AspNetCore.Hosting;

namespace Biogarden.WebApi.Tests;

public sealed class TestHostIsolationTests
{
    [Fact]
    public async Task DisposingAnotherFactory_DoesNotAffectHostStartupOrProcessEnvironment()
    {
        var originalSecret = Environment.GetEnvironmentVariable("Jwt__SecretKey");
        using var pendingFactory = new BiogardenWebApiFactory();
        using (var otherFactory = new BiogardenWebApiFactory())
        {
            using var otherClient = otherFactory.CreateClient();
            using var response = await otherClient.GetAsync("/health/live");
            response.EnsureSuccessStatusCode();
            Assert.True(originalSecret == Environment.GetEnvironmentVariable("Jwt__SecretKey"), "Creating a test host must not change the process JWT setting.");
        }

        Assert.True(originalSecret == Environment.GetEnvironmentVariable("Jwt__SecretKey"), "Disposing a test host must not change the process JWT setting.");

        // Match the derived factories used by endpoint tests, without Development secrets.
        using var derivedFactory = pendingFactory.WithWebHostBuilder(webHostBuilder => webHostBuilder.UseEnvironment("Testing"));
        using var client = derivedFactory.CreateClient();
        using var result = await client.GetAsync("/health/live");
        result.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", await result.Content.ReadAsStringAsync());
    }
}
