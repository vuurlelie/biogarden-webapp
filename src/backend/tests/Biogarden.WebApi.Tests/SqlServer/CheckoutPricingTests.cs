using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Constants;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        await database.Products.Where(product => product.Id == productId).ExecuteUpdateAsync(updateSettersBuilder => updateSettersBuilder.SetProperty(product => product.Price, 9999));
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
        await database.Products.Where(product => product.Id == productId).ExecuteUpdateAsync(updateSettersBuilder => updateSettersBuilder.SetProperty(product => product.Price, 5100));
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
    public async Task QuoteIsSessionBoundAndRejectsInvalidOrIncompatibleMethods()
    {
        using var owner = _app.Factory.CreateClient();
        using var stranger = _app.Factory.CreateClient();
        await AddProductAsync(owner);
        using var missing = await stranger.PostAsJsonAsync("/api/orders/quote", new CheckoutQuoteRequest(FulfillmentMethod.HomeDelivery, OrderPaymentMethod.OnlineCard));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        foreach (var choice in new[] {
            new CheckoutQuoteRequest(FulfillmentMethod.HomeDelivery, OrderPaymentMethod.PayAtPickup),
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
    public async Task ReceiptRequiresOwningSessionAndOfflineOrdersCannotStartOnlinePayment()
    {
        using var owner = _app.Factory.CreateClient();
        using var stranger = _app.Factory.CreateClient();
        await AddProductAsync(owner);
        using var created = await owner.PostAsJsonAsync("/api/orders", await Request().WithQuoteAsync(owner));
        created.EnsureSuccessStatusCode();
        var order = (await created.Content.ReadFromJsonAsync<OrderDto>())!;
        using var receipt = await owner.GetAsync($"/api/orders/{order.Id}/receipt");
        receipt.EnsureSuccessStatusCode();
        Assert.True(receipt.Headers.CacheControl!.NoStore);
        Assert.Equal(8340, (await receipt.Content.ReadFromJsonAsync<OrderDto>())!.TotalPrice);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/orders/{order.Id}/receipt")).StatusCode);
        using var blocked = await owner.PostAsync($"/api/orders/{order.Id}/sumup-checkout", null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("nem online", await blocked.Content.ReadAsStringAsync());
        await using var databaseContext = _app.OpenDatabase();
        Assert.Null((await databaseContext.Orders.SingleAsync()).SumUpCheckoutId);
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
        Assert.Null(legacy.GlsDeliveryPointId);
        Assert.Null(legacy.GlsDeliveryPointName);
        Assert.Null(legacy.GlsDeliveryPointAddress);
        Assert.Null(legacy.GlsDeliveryPointCountryCode);
        Assert.Equal(5000, (await database.OrderItems.SingleAsync()).LineTotal);
    }

    public static IEnumerable<object[]> PurchaseCombinations()
    {
        foreach (var company in new[] { false, true })
            foreach (var method in Enum.GetValues<FulfillmentMethod>())
                foreach (var payment in new[] { OrderPaymentMethod.OnlineCard,
            method == FulfillmentMethod.PersonalPickup ? OrderPaymentMethod.PayAtPickup : OrderPaymentMethod.CashOnDelivery })
                    yield return [method, payment, company];
    }

    [SqlServerTheory]
    [MemberData(nameof(PurchaseCombinations))]
    public async Task EverySupportedPurchasePersistsReviewedTotalsDespiteInjectedAmounts(FulfillmentMethod method, OrderPaymentMethod payment, bool company)
    {
        using var client = _app.Factory.CreateClient();
        await AddProductAsync(client);
        var request = Request();
        request.FulfillmentMethod = method;
        request.PaymentMethod = payment;
        if (method is FulfillmentMethod.ParcelLocker or FulfillmentMethod.ParcelShop)
        {
            var point = new GlsDeliveryPointDto("HU-MATRIX", method, "Matrix point", "HU", "8200", "Veszprém", "Test utca 12.", true, true);
            _app.Gls.Points[point.Id] = point;
            request.GlsDeliveryPointId = point.Id;
        }
        if (company)
        {
            request.CustomerType = CustomerType.Company;
            request.BillingCompanyName = "Matrix Kft.";
            request.BillingTaxNumber = "12345678-1-19";
            request.PostalCode = "8200";
            request.City = "Veszprém";
            request.Street = "Billing";
            request.HouseNumber = "2";
        }
        using var quoted = await client.PostAsJsonAsync("/api/orders/quote", new CheckoutQuoteRequest(method, payment, request.GlsDeliveryPointId));
        quoted.EnsureSuccessStatusCode();
        var quote = (await quoted.Content.ReadFromJsonAsync<CheckoutQuoteDto>())!;
        request.QuoteVersion = quote.Version;
        var payload = JsonSerializer.SerializeToNode(request, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        payload["totalPrice"] = 1;
        payload["subtotal"] = 1;
        payload["deliveryFee"] = 0;
        payload["paymentFee"] = 0;
        payload["currency"] = "EUR";
        using var created = await client.PostAsJsonAsync("/api/orders", payload);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var order = (await created.Content.ReadFromJsonAsync<OrderDto>())!;
        var deliveryFee = method switch
        {
            FulfillmentMethod.PersonalPickup => 0,
            FulfillmentMethod.HomeDelivery => 2740,
            FulfillmentMethod.ParcelLocker => 2090,
            _ => 2590
        };
        Assert.Equal(5000 + deliveryFee + (payment == OrderPaymentMethod.CashOnDelivery ? 600 : 0), quote.TotalPrice);
        Assert.Equal(quote.TotalPrice, order.TotalPrice);
        Assert.Equal(quote.Subtotal, order.Subtotal);
        Assert.Equal(quote.DeliveryFee, order.DeliveryFee);
        Assert.Equal(quote.PaymentFee, order.PaymentFee);
        Assert.Equal("HUF", order.Currency);
        Assert.Equal(payment, order.PaymentMethod);
        await using var databaseContext = _app.OpenDatabase();
        var saved = await databaseContext.Orders.SingleAsync();
        Assert.Equal(order.TotalPrice, saved.TotalPrice);
        Assert.Equal(request.CustomerType, saved.CustomerType);
        Assert.Equal(company ? "Matrix Kft." : null, saved.BillingCompanyName);
        Assert.Equal(2, (await databaseContext.Products.SingleAsync()).StockQuantity);
        Assert.Equal(2, await databaseContext.OrderEmails.CountAsync());
    }

    [SqlServerFact]
    public async Task ConfigurationChangeRejectsReviewedOrderAndDisabledMethodCannotBeSubmitted()
    {
        using var client = _app.Factory.CreateClient();
        await AddProductAsync(client);
        var request = await Request().WithQuoteAsync(client);
        // Simulates a configuration change between review and submission without changing the database.
        var config = _app.Factory.Services.GetRequiredService<IOptions<CheckoutPricingOptions>>().Value;
        config.DeliveryFees[FulfillmentMethod.HomeDelivery] = 2800;
        using var changed = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        await request.WithQuoteAsync(client);
        config.DeliveryFees.Remove(FulfillmentMethod.HomeDelivery);
        using var disabled = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Conflict, disabled.StatusCode);
        var methods = (await client.GetFromJsonAsync<CheckoutMethodDto[]>("/api/orders/checkout-methods"))!;
        Assert.DoesNotContain(methods, checkoutMethod => checkoutMethod.FulfillmentMethod == FulfillmentMethod.HomeDelivery);
        await using var databaseContext = _app.OpenDatabase();
        Assert.Empty(await databaseContext.Orders.ToListAsync());
        Assert.Empty(await databaseContext.OrderEmails.ToListAsync());
        Assert.Equal(3, (await databaseContext.Products.SingleAsync()).StockQuantity);
        Assert.False((await databaseContext.Carts.SingleAsync()).IsCheckedOut);
    }

    [SqlServerFact]
    public async Task PaymentDisabledAfterReviewIsNeitherOfferedNorAccepted()
    {
        using var client = _app.Factory.CreateClient();
        await AddProductAsync(client);
        var request = await Request().WithQuoteAsync(client);
        var config = _app.Factory.Services.GetRequiredService<IOptions<CheckoutPricingOptions>>().Value;
        var index = Array.IndexOf(config.EnabledPaymentMethods, OrderPaymentMethod.CashOnDelivery);
        Assert.True(index >= 0);
        config.EnabledPaymentMethods[index] = OrderPaymentMethod.OnlineCard;
        using var disabled = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Conflict, disabled.StatusCode);
        using var quote = await client.PostAsJsonAsync("/api/orders/quote", new CheckoutQuoteRequest(request.FulfillmentMethod, request.PaymentMethod!.Value));
        Assert.Equal(HttpStatusCode.Conflict, quote.StatusCode);
        var methods = (await client.GetFromJsonAsync<CheckoutMethodDto[]>("/api/orders/checkout-methods"))!;
        Assert.All(methods, method => Assert.DoesNotContain(OrderPaymentMethod.CashOnDelivery, method.PaymentMethods));
        await using var databaseContext = _app.OpenDatabase();
        Assert.Empty(await databaseContext.Orders.ToListAsync());
        Assert.Empty(await databaseContext.OrderEmails.ToListAsync());
        Assert.Equal(3, (await databaseContext.Products.SingleAsync()).StockQuantity);
    }
}
