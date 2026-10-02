using System.Diagnostics;
using System.Net;
using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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

    [RealBrowserTheory]
    [InlineData("chromium-real")]
    [InlineData("firefox-real")]
    [InlineData("mobile-real")]
    public async Task CheckoutSurvivesLostResponseAndCancellationRestoresStock(string browserProject)
    {
        using var certificate = new BrowserCertificate();
        await using var factory = _app.Factory.WithWebHostBuilder(builder => builder.UseSetting("urls", "https://localhost:5097").ConfigureTestServices(services =>
            services.Configure<CorsOptions>(options => options.AddPolicy("FrontendDev", policy =>
                policy.WithOrigins("https://localhost:4174").AllowAnyHeader().AllowAnyMethod().AllowCredentials()))));
        factory.UseKestrel(options => options.ListenLocalhost(5097, listen => listen.UseHttps(certificate.Value)));
        using var client = factory.CreateClient();
        var product = new Product { Name = "Real journey tea", Available = true, DeliveryAvailable = true, Price = 900m, StockQuantity = 10 };
        var expiredCart = new Cart { Id = Guid.NewGuid(), SessionId = Guid.NewGuid().ToString(), ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) };
        expiredCart.Items.Add(new CartItem { Id = Guid.NewGuid(), Product = product, Quantity = 1 });
        var unavailable = new Product { Name = "Unavailable test product", Available = false, Price = 100, StockQuantity = 10 };
        var published = new Post { Title = "Browser public article", Article = "<p>Public content</p>" };
        var draft = new Post { Title = "Browser private draft", Article = "<p>Private draft</p>", Draft = true };
        var deleted = new Post { Title = "Browser deleted article", Article = "<p>Deleted content</p>", RecordStatus = RecordStatus.Deleted };
        await using (var db = _app.OpenDatabase())
        {
            db.Products.AddRange(product, unavailable);
            db.Carts.Add(expiredCart);
            db.Posts.AddRange(published, draft, deleted);
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
        var start = new ProcessStartInfo("node") { WorkingDirectory = frontend, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, CreateNoWindow = true };
        start.ArgumentList.Add("node_modules/@playwright/test/cli.js");
        start.ArgumentList.Add("test");
        start.ArgumentList.Add("--config=playwright.real.config.ts");
        start.ArgumentList.Add($"--project={browserProject}");
        start.Environment["E2E_API_URL"] = client.BaseAddress!.ToString().TrimEnd('/');
        start.Environment["E2E_BROWSER_PROJECT"] = browserProject;
        start.Environment["E2E_TLS_CERT"] = certificate.CertificatePath;
        start.Environment["E2E_TLS_KEY"] = certificate.KeyPath;
        start.Environment["E2E_EXPIRED_SESSION"] = expiredCart.SessionId;
        start.Environment["E2E_EXPIRED_CART_ID"] = expiredCart.Id.ToString();
        start.Environment["E2E_UNAVAILABLE_ID"] = unavailable.Id.ToString();
        start.Environment["E2E_PUBLISHED_ID"] = published.Id.ToString();
        start.Environment["E2E_DRAFT_ID"] = draft.Id.ToString();
        start.Environment["E2E_DELETED_ID"] = deleted.Id.ToString();
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
        Assert.Equal(10, (await check.Products.SingleAsync(x => x.Id == product.Id)).StockQuantity);
        Assert.Equal(2, await check.OrderEmails.CountAsync());
        Assert.Empty(await check.PaymentAttempts.ToListAsync());
        Assert.Empty(_app.Sender.Sent);
    }
}

public sealed class RealBrowserTheoryAttribute : TheoryAttribute
{
    public RealBrowserTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("BIOGARDEN_REAL_BROWSER") != "1" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerFactAttribute.ConnectionVariable)))
            Skip = "Set BIOGARDEN_REAL_BROWSER=1 and BIOGARDEN_TEST_SQLSERVER; install frontend dependencies and Playwright Chromium/Firefox.";
    }
}

internal sealed class BrowserCertificate : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"BiogardenBrowser-{Guid.NewGuid():N}");
    public X509Certificate2 Value { get; }
    public string CertificatePath => Path.Combine(_directory, "certificate.pem");
    public string KeyPath => Path.Combine(_directory, "key.pem");
    public BrowserCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        Value = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.DefaultKeySet);
        Directory.CreateDirectory(_directory);
        File.WriteAllText(CertificatePath, generated.ExportCertificatePem());
        File.WriteAllText(KeyPath, rsa.ExportPkcs8PrivateKeyPem());
    }
    public void Dispose()
    {
        Value.Dispose();
        File.Delete(CertificatePath);
        File.Delete(KeyPath);
        Directory.Delete(_directory); // Only the two files created above; never recursive.
    }
}
