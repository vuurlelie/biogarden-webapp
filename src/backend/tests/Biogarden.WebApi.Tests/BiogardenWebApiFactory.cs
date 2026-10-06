using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Filters;
using Biogarden.WebApi.Filters;

namespace Biogarden.WebApi.Tests;

public sealed class BiogardenWebApiFactory : WebApplicationFactory<Program>
{
    private const string TestJwtSecretKey =
        "TestJwtSecretKeyForWebApiIntegrationTests12345";

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add("X-Checkout-Request", "1");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Startup validates JWT settings before the app configuration callback runs.
        // Host settings provide the key early without sharing mutable process state.
        builder.UseSetting("Jwt:SecretKey", TestJwtSecretKey);
        // These HTTP contract tests replace business services/authentication as needed.
        // SQL/JWT tests use SqlServerApp and the real account-security filter instead.
        builder.ConfigureTestServices(services => services.AddScoped<AccountSecurityMutationFilter, ContractTestAccountFilter>());
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = TestJwtSecretKey,
                ["OrderEmailOutbox:Enabled"] = "false",
                ["NotificationEmailOutbox:Enabled"] = "false",
                ["CartCleanup:Enabled"] = "false",
                ["CheckoutDrafts:CleanupEnabled"] = "false"
            });
        });
    }

    private sealed class ContractTestAccountFilter : AccountSecurityMutationFilter
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
            => await next();
    }
}
