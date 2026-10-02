using System.Diagnostics;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

// Opt-in: launches a real browser, Vite and a loopback Kestrel API against a disposable SQL database.
[Trait("Category", "RealBrowser")]
public sealed class RealBrowserJourneyTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [RealBrowserFact]
    public async Task CheckoutSurvivesLostResponseAndCancellationRestoresStock()
    {
        await using var factory = _app.Factory.WithWebHostBuilder(builder => builder.UseSetting("urls", "http://localhost:5097").ConfigureTestServices(services =>
            services.Configure<CorsOptions>(options => options.AddPolicy("FrontendDev", policy =>
                policy.WithOrigins("http://localhost:4174").AllowAnyHeader().AllowAnyMethod().AllowCredentials()))));
        factory.UseKestrel(5097);
        using var client = factory.CreateClient();
        var product = new Product { Name = "Real journey tea", Available = true, DeliveryAvailable = true, Price = 900m, StockQuantity = 10 };
        await using (var db = _app.OpenDatabase())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }
        var email = $"browser-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                FirstName = "Test",
                LastName = "Customer",
                PhoneNumber = "+36301234567"
            };
            Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, "Password123!")).Succeeded);
            await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync(user.Id, null,
                new AddCartItemDto { ProductId = product.Id, Quantity = 1 });
        }
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "frontend", "biogarden-client")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var frontend = Path.Combine(directory.FullName, "frontend", "biogarden-client");
        var start = new ProcessStartInfo("node") { WorkingDirectory = frontend, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add("node_modules/@playwright/test/cli.js");
        start.ArgumentList.Add("test");
        start.ArgumentList.Add("--config=playwright.real.config.ts");
        start.Environment["E2E_API_URL"] = client.BaseAddress!.ToString().TrimEnd('/');
        start.Environment["E2E_PRODUCT_ID"] = product.Id.ToString();
        start.Environment["E2E_CUSTOMER_EMAIL"] = email;
        start.Environment.Remove(SqlServerFactAttribute.ConnectionVariable);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3)); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
        Assert.True(process.ExitCode == 0, (await output) + (await error));
        await using var check = _app.OpenDatabase();
        var order = await check.Orders.Include(x => x.Items).SingleAsync();
        Assert.Equal(1800m, order.TotalPrice);
        Assert.Equal(2, Assert.Single(order.Items).Quantity);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.False(order.StockCommitted);
        Assert.Equal(10, (await check.Products.SingleAsync()).StockQuantity);
        Assert.Equal(2, await check.OrderEmails.CountAsync());
        Assert.Empty(await check.PaymentAttempts.ToListAsync());
        Assert.Empty(_app.Sender.Sent);
    }
}

public sealed class RealBrowserFactAttribute : FactAttribute
{
    public RealBrowserFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("BIOGARDEN_REAL_BROWSER") != "1" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerFactAttribute.ConnectionVariable)))
            Skip = "Set BIOGARDEN_REAL_BROWSER=1 and BIOGARDEN_TEST_SQLSERVER; install frontend dependencies and Playwright Chromium.";
    }
}
