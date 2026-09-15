using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Biogarden.WebApi.Tests;

public sealed class BiogardenWebApiFactory : WebApplicationFactory<Program>
{
    private const string TestJwtSecretKey =
        "TestJwtSecretKeyForWebApiIntegrationTests12345";

    public BiogardenWebApiFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__SecretKey", TestJwtSecretKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = TestJwtSecretKey,
                ["OrderEmailOutbox:Enabled"] = "false"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable("Jwt__SecretKey", null);

        base.Dispose(disposing);
    }
}
