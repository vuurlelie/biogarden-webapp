using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Biogarden.WebApi.Tests;

public sealed class BiogardenWebApiFactory : WebApplicationFactory<Program>
{
    private const string TestJwtSecretKey =
        "TestJwtSecretKeyForWebApiIntegrationTests12345";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Startup validates JWT settings before the app configuration callback runs.
        // Host settings provide the key early without sharing mutable process state.
        builder.UseSetting("Jwt:SecretKey", TestJwtSecretKey);
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = TestJwtSecretKey,
                ["OrderEmailOutbox:Enabled"] = "false"
            });
        });
    }
}
