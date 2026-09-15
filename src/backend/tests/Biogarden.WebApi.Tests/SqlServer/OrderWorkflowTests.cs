using System.Net;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.Constants;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class OrderWorkflowTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task GuestCheckout_PersistsOneOrderAndTwoEmails_WhenEmailMatchesAccount()
    {
        using (var scope = _app.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var result = await users.CreateAsync(new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "customer@example.com",
                Email = "customer@example.com",
                EmailConfirmed = true
            });
            Assert.True(result.Succeeded);
        }
        _app.Sender.Fail = true;
        var productId = await SeedProductAsync();
        using var client = _app.Factory.CreateClient();
        await AddToCartAsync(client, productId);
        var request = Request();
        using var first = await client.PostAsJsonAsync("/api/orders", request);
        first.EnsureSuccessStatusCode();
        var order = (await first.Content.ReadFromJsonAsync<OrderDto>())!;
        using var retry = await client.PostAsJsonAsync("/api/orders", request);
        retry.EnsureSuccessStatusCode();
        Assert.Equal(order.Id, (await retry.Content.ReadFromJsonAsync<OrderDto>())!.Id);

        await using var database = _app.OpenDatabase();
        var saved = await database.Orders.Include(x => x.Items).SingleAsync();
        Assert.Null(saved.UserId);
        Assert.Equal(900m, saved.TotalPrice);
        Assert.Single(saved.Items);
        Assert.Equal(2, (await database.Products.SingleAsync()).StockQuantity);
        Assert.True((await database.Carts.SingleAsync()).IsCheckedOut);
        Assert.Equal(2, await database.OrderEmails.CountAsync());
        Assert.Empty(_app.Sender.Sent);

        using var stranger = _app.Factory.CreateClient();
        using var unauthorizedRetry = await stranger.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.NotFound, unauthorizedRetry.StatusCode);
    }

    [SqlServerFact]
    public async Task QueuePersistenceFailure_RollsBackOrderStockAndCart()
    {
        var productId = await SeedProductAsync();
        using var client = _app.Factory.CreateClient();
        await AddToCartAsync(client, productId);
        _app.QueueFailure.Enabled = true;
        using var failed = await client.PostAsJsonAsync("/api/orders", Request());
        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);

        await using var database = _app.OpenDatabase();
        Assert.Empty(await database.Orders.ToListAsync());
        Assert.Empty(await database.OrderEmails.ToListAsync());
        Assert.Equal(3, (await database.Products.SingleAsync()).StockQuantity);
        Assert.False((await database.Carts.SingleAsync()).IsCheckedOut);
    }

    [SqlServerFact]
    public async Task EmailRetry_SurvivesApiRestartAndClearsDeliveredPayloads()
    {
        await PlaceOrderAsync();
        _app.Sender.Fail = true;
        using (var scope = _app.Factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<OrderEmailDispatcher>();
            Assert.True(await dispatcher.DispatchNextAsync(default));
            Assert.True(await dispatcher.DispatchNextAsync(default));
            Assert.False(await dispatcher.DispatchNextAsync(default));
        }
        await using (var database = _app.OpenDatabase())
        {
            var queued = await database.OrderEmails.ToListAsync();
            Assert.All(queued, email =>
            {
                Assert.Null(email.SentAtUtc);
                Assert.Equal(1, email.Attempts);
                Assert.Null(email.LeaseId);
                Assert.False(string.IsNullOrEmpty(email.HtmlBody));
            });
            await database.OrderEmails.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        }
        await _app.RestartApiAsync();
        _app.Sender.Fail = false;
        using (var scope = _app.Factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<OrderEmailDispatcher>();
            Assert.True(await dispatcher.DispatchNextAsync(default));
            Assert.True(await dispatcher.DispatchNextAsync(default));
            Assert.False(await dispatcher.DispatchNextAsync(default));
        }
        Assert.Equal(2, _app.Sender.Sent.Count);
        Assert.Equal(2, _app.Sender.Sent.Select(x => x.To).Distinct().Count());
        await using var check = _app.OpenDatabase();
        Assert.All(await check.OrderEmails.ToListAsync(), email =>
        {
            Assert.NotNull(email.SentAtUtc);
            Assert.Equal(2, email.Attempts);
            Assert.Empty(email.Recipient);
            Assert.Null(email.HtmlBody);
            Assert.Null(email.PlainTextBody);
        });
    }

    [SqlServerFact]
    public async Task ConcurrentClaims_GrantOnlyOneLease_AndExpiredLeaseCannotCompleteNewClaim()
    {
        await PlaceOrderAsync();
        await using (var database = _app.OpenDatabase())
        {
            await database.OrderEmails.Where(x => x.Kind == "business").ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.NextAttemptAtUtc, DateTime.UtcNow.AddHours(1)));
        }
        using var firstScope = _app.Factory.Services.CreateScope();
        using var secondScope = _app.Factory.Services.CreateScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IOrderEmailRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IOrderEmailRepository>();
        var now = DateTime.UtcNow;
        var claims = await Task.WhenAll(firstRepository.TryClaimAsync(now, default), secondRepository.TryClaimAsync(now, default));
        var firstClaim = Assert.Single(claims, x => x is not null)!;
        var newClaim = await secondRepository.TryClaimAsync(now.AddMinutes(3), default);
        Assert.NotNull(newClaim);
        Assert.Equal(firstClaim.Id, newClaim.Id);
        Assert.NotEqual(firstClaim.LeaseId, newClaim.LeaseId);

        await firstRepository.CompleteAsync(firstClaim.Id, firstClaim.LeaseId!.Value, now, default);
        await using var check = _app.OpenDatabase();
        var pending = await check.OrderEmails.AsNoTracking().SingleAsync(x => x.Id == newClaim.Id);
        Assert.Null(pending.SentAtUtc);
        Assert.Equal(newClaim.LeaseId, pending.LeaseId);
        await secondRepository.CompleteAsync(newClaim.Id, newClaim.LeaseId!.Value, now.AddMinutes(3), default);
        Assert.NotNull((await check.OrderEmails.AsNoTracking().SingleAsync(x => x.Id == newClaim.Id)).SentAtUtc);
    }

    [SqlServerFact]
    public async Task ConcurrentInventoryUpdates_RejectStaleRowVersion()
    {
        var id = await SeedProductAsync();
        await using var first = _app.OpenDatabase();
        await using var second = _app.OpenDatabase();
        var a = await first.Products.SingleAsync(x => x.Id == id);
        var b = await second.Products.SingleAsync(x => x.Id == id);
        a.StockQuantity = 2;
        b.StockQuantity = 1;
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var check = _app.OpenDatabase();
        Assert.Equal(2, (await check.Products.SingleAsync(x => x.Id == id)).StockQuantity);
    }

    private async Task<int> SeedProductAsync()
    {
        await using var database = _app.OpenDatabase();
        var product = new Product { Name = "Integration test product", Available = true, Price = 900m, StockQuantity = 3 };
        database.Products.Add(product);
        await database.SaveChangesAsync();
        return product.Id;
    }

    private static async Task AddToCartAsync(HttpClient client, int productId)
    {
        using var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = productId, Quantity = 1 });
        response.EnsureSuccessStatusCode();
    }

    private async Task PlaceOrderAsync()
    {
        var productId = await SeedProductAsync();
        using var client = _app.Factory.CreateClient();
        await AddToCartAsync(client, productId);
        using var response = await client.PostAsJsonAsync("/api/orders", Request());
        response.EnsureSuccessStatusCode();
    }

    private static PlaceOrderDto Request() => new()
    {
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        CustomerName = "Test Customer",
        CustomerEmail = "customer@example.com",
        CustomerPhone = "+361234567",
        FulfillmentMethod = FulfillmentMethod.PersonalPickup,
        AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
        AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion
    };
}
