using System.Net;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.Constants;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class CheckoutPricingTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    private async Task<int> AddProductAsync(HttpClient client)
    {
        await using var database = _app.OpenDatabase();
        var product = new Product
        {
            Name = "Pricing tea",
            Available = true,
            DeliveryAvailable = true,
            Price = 5000,
            StockQuantity = 3
        };
        database.Products.Add(product);
        await database.SaveChangesAsync();
        using var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = product.Id, Quantity = 1 });
        response.EnsureSuccessStatusCode();
        return product.Id;
    }

    private static PlaceOrderDto Request() => new()
    {
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        FulfillmentMethod = FulfillmentMethod.HomeDelivery,
        PaymentMethod = OrderPaymentMethod.CashOnDelivery,
        CustomerName = "Pricing Customer",
        CustomerEmail = "pricing@example.com",
        CustomerPhone = "+36301234567",
        DeliveryPostalCode = "8200",
        DeliveryCity = "Veszprém",
        DeliveryStreet = "Test",
        DeliveryHouseNumber = "1",
        AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
        AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion
    };

    [SqlServerFact]
    public async Task DeliveryQuoteIsPersistedAndRetryDoesNotRepriceOrDuplicateEffects()
    {
        using var client = _app.Factory.CreateClient();
        var productId = await AddProductAsync(client);
        var request = await Request().WithQuoteAsync(client);
        using var response = await client.PostAsJsonAsync("/api/orders", request);
        response.EnsureSuccessStatusCode();
        var order = (await response.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal(5000, order.Subtotal);
        Assert.Equal(2740, order.DeliveryFee);
        Assert.Equal(600, order.PaymentFee);
        Assert.Equal(8340, order.TotalPrice);
        Assert.Equal(OrderPaymentMethod.CashOnDelivery, order.PaymentMethod);
        Assert.Equal("HUF", order.Currency);
        await using var database = _app.OpenDatabase();
        await database.Products.Where(x => x.Id == productId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Price, 9999));
        request.QuoteVersion = null;
        request.PaymentMethod = null;
        using var retry = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(8340, (await retry.Content.ReadFromJsonAsync<OrderDto>())!.TotalPrice);
        Assert.Equal(2, (await database.Products.SingleAsync()).StockQuantity);
        Assert.Single(await database.Orders.ToListAsync());
        Assert.Equal(2, await database.OrderEmails.CountAsync());
    }

    [SqlServerFact]
    public async Task StaleQuoteRollsBackAndFreshReviewCanSucceed()
    {
        using var client = _app.Factory.CreateClient();
        var productId = await AddProductAsync(client);
        var request = await Request().WithQuoteAsync(client);
        await using var database = _app.OpenDatabase();
        await database.Products.Where(x => x.Id == productId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Price, 5100));
        using var stale = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Empty(await database.Orders.ToListAsync());
        Assert.Empty(await database.OrderEmails.ToListAsync());
        Assert.Equal(3, (await database.Products.SingleAsync()).StockQuantity);
        Assert.False((await database.Carts.SingleAsync()).IsCheckedOut);
        await request.WithQuoteAsync(client);
        using var updated = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Created, updated.StatusCode);
        Assert.Equal(8440, (await updated.Content.ReadFromJsonAsync<OrderDto>())!.TotalPrice);
    }

    [SqlServerFact]
    public async Task QuoteIsSessionBoundAndRejectsDisabledOrIncompatibleMethods()
    {
        using var owner = _app.Factory.CreateClient();
        using var stranger = _app.Factory.CreateClient();
        await AddProductAsync(owner);
        using var missing = await stranger.PostAsJsonAsync("/api/orders/quote", new CheckoutQuoteRequest(
            FulfillmentMethod.HomeDelivery, OrderPaymentMethod.OnlineCard));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        foreach (var choice in new[] {
            new CheckoutQuoteRequest(FulfillmentMethod.HomeDelivery, OrderPaymentMethod.PayAtPickup),
            new CheckoutQuoteRequest(FulfillmentMethod.ParcelShop, OrderPaymentMethod.OnlineCard),
            new CheckoutQuoteRequest((FulfillmentMethod)99, OrderPaymentMethod.OnlineCard) })
        {
            using var response = await owner.PostAsJsonAsync("/api/orders/quote", choice);
            Assert.Equal(Enum.IsDefined(choice.FulfillmentMethod) ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, response.StatusCode);
        }
        var request = Request();
        using var noQuote = await owner.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.BadRequest, noQuote.StatusCode);
        request.QuoteVersion = new string('A', 64);
        using var forged = await owner.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Conflict, forged.StatusCode);
        await using var database = _app.OpenDatabase();
        Assert.Empty(await database.Orders.ToListAsync());
        Assert.Equal(3, (await database.Products.SingleAsync()).StockQuantity);
    }

    [SqlServerFact]
    public async Task MigrationPreservesLegacyTotalsWithoutInventingFeeDetails()
    {
        using var client = _app.Factory.CreateClient();
        await AddProductAsync(client);
        using var created = await client.PostAsJsonAsync("/api/orders", await Request().WithQuoteAsync(client));
        created.EnsureSuccessStatusCode();
        // Only this fixture's disposable database is migrated backwards to simulate existing data.
        await using var database = _app.OpenDatabase();
        var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260929112302_AddCheckoutDrafts");
        await migrator.MigrateAsync();
        var legacy = await database.Orders.AsNoTracking().SingleAsync();
        Assert.Equal(8340, legacy.TotalPrice);
        Assert.Null(legacy.Subtotal);
        Assert.Null(legacy.DeliveryFee);
        Assert.Null(legacy.PaymentFee);
        Assert.Null(legacy.PaymentMethod);
        Assert.Null(legacy.Currency);
        Assert.Equal(5000, (await database.OrderItems.SingleAsync()).LineTotal);
    }
}
