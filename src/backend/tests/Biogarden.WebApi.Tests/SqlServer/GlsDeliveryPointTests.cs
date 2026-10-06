using System.Net;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.Constants;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.DTOs.Checkout;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class GlsDeliveryPointTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();
    private GlsDeliveryPointDto Point(FulfillmentMethod type = FulfillmentMethod.ParcelLocker)
    {
        var point = new GlsDeliveryPointDto("HU-TEST", type, "GLS test point", "HU", "8200", "Veszprém", "Test utca 12.", false, true);
        _app.Gls.Points[point.Id] = point;
        return point;
    }
    private async Task SeedAsync(HttpClient client)
    {
        await using var db = _app.OpenDatabase();
        var product = new Product { Name = "GLS tea", Price = 5000, Available = true, DeliveryAvailable = true, StockQuantity = 3 };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        (await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = product.Id, Quantity = 1 })).EnsureSuccessStatusCode();
    }
    private static PlaceOrderDto Request(GlsDeliveryPointDto point) => new()
    {
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        FulfillmentMethod = point.Type,
        PaymentMethod = OrderPaymentMethod.CashOnDelivery,
        GlsDeliveryPointId = point.Id,
        CustomerName = "GLS customer",
        CustomerEmail = "gls@example.com",
        CustomerPhone = "+36301234567",
        DeliveryPostalCode = "9999",
        DeliveryCity = "Forged city",
        DeliveryStreet = "Forged street",
        DeliveryHouseNumber = "99",
        AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
        AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion
    };

    [SqlServerFact]
    public Task LockerOrderPersistsAuthoritativeSnapshotAndRetriesDuringOutage()
        => VerifyOrderAsync(FulfillmentMethod.ParcelLocker);

    [SqlServerFact]
    public Task ShopOrderPersistsAuthoritativeSnapshotAndRetriesDuringOutage()
        => VerifyOrderAsync(FulfillmentMethod.ParcelShop);

    private async Task VerifyOrderAsync(FulfillmentMethod type)
    {
        using var client = _app.Factory.CreateClient();
        _app.Gls.Unavailable = false;
        var point = Point(type);
        await SeedAsync(client);
        var request = await Request(point).WithQuoteAsync(client);
        using var created = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var order = (await created.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal(point.Id, order.GlsDeliveryPointId);
        Assert.Equal(point.Name, order.GlsDeliveryPointName);
        Assert.Equal(point.Address, order.GlsDeliveryPointAddress);
        Assert.Equal(point.Address, order.DeliveryStreet);
        Assert.Equal(point.City, order.DeliveryCity);
        Assert.Null(order.DeliveryHouseNumber);
        Assert.Equal(type == FulfillmentMethod.ParcelLocker ? 2090 : 2590, order.DeliveryFee);
        _app.Gls.Unavailable = true;
        using var retry = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(order.Id, (await retry.Content.ReadFromJsonAsync<OrderDto>())!.Id);
        await using var db = _app.OpenDatabase();
        Assert.Equal(1, await db.Orders.CountAsync());
        Assert.Equal(2, await db.OrderEmails.CountAsync());
        Assert.All(await db.Products.ToListAsync(), product => Assert.Equal(2, product.StockQuantity));
    }

    [SqlServerFact]
    public async Task InvalidChangedAndUnavailablePointsCannotCreateOrdersOrConsumeStock()
    {
        using var client = _app.Factory.CreateClient();
        await SeedAsync(client);
        var point = Point();
        var request = await Request(point).WithQuoteAsync(client);
        using var missing = await client.PostAsJsonAsync("/api/orders/quote", new CheckoutQuoteRequest(point.Type, OrderPaymentMethod.OnlineCard));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var mismatch = await client.PostAsJsonAsync("/api/orders/quote", new CheckoutQuoteRequest(FulfillmentMethod.ParcelShop, OrderPaymentMethod.OnlineCard, point.Id));
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        _app.Gls.Points[point.Id] = point with { Address = "Moved street 1." };
        using var changed = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        _app.Gls.Points.Clear();
        using var removed = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Conflict, removed.StatusCode);
        _app.Gls.Unavailable = true;
        using var unavailable = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        using var lookup = await client.GetAsync($"/api/shipping/gls/points/{point.Id}?type={(int)point.Type}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, lookup.StatusCode);
        using var pickup = await client.PostAsJsonAsync("/api/orders/quote", new CheckoutQuoteRequest(FulfillmentMethod.PersonalPickup, OrderPaymentMethod.PayAtPickup));
        pickup.EnsureSuccessStatusCode();
        await using var db = _app.OpenDatabase();
        Assert.Empty(await db.Orders.ToListAsync());
        Assert.Empty(await db.OrderEmails.ToListAsync());
        Assert.Equal(3, (await db.Products.SingleAsync()).StockQuantity);
        Assert.False((await db.Carts.SingleAsync()).IsCheckedOut);
    }

    [SqlServerFact]
    public async Task CashOnDeliveryRequiresPointPaymentCapability()
    {
        using var client = _app.Factory.CreateClient();
        await SeedAsync(client);
        var point = Point() with { AcceptsCard = false, AcceptsCash = false };
        _app.Gls.Points[point.Id] = point;
        using var cod = await client.PostAsJsonAsync("/api/orders/quote", new CheckoutQuoteRequest(point.Type, OrderPaymentMethod.CashOnDelivery, point.Id));
        Assert.Equal(HttpStatusCode.Conflict, cod.StatusCode);
        using var online = await client.PostAsJsonAsync("/api/orders/quote", new CheckoutQuoteRequest(point.Type, OrderPaymentMethod.OnlineCard, point.Id));
        online.EnsureSuccessStatusCode();
        using var lookup = await client.GetAsync($"/api/shipping/gls/points/{point.Id}?type={(int)point.Type}");
        lookup.EnsureSuccessStatusCode();
        Assert.True(lookup.Headers.CacheControl!.NoStore);
    }

    [SqlServerFact]
    public async Task ReplacingValidPointRequiresFreshReviewAndOutageRecoveryCanCompleteOnce()
    {
        using var client = _app.Factory.CreateClient();
        await SeedAsync(client);
        var original = Point();
        var replacement = original with { Id = "HU-SECOND", Name = "Second locker", Address = "Other street 3." };
        _app.Gls.Points[replacement.Id] = replacement;
        var request = await Request(original).WithQuoteAsync(client);
        request.GlsDeliveryPointId = replacement.Id;
        using var tampered = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Conflict, tampered.StatusCode);
        _app.Gls.Unavailable = true;
        using var outage = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, outage.StatusCode);
        await using var db = _app.OpenDatabase();
        Assert.Empty(await db.Orders.ToListAsync());
        Assert.Empty(await db.OrderEmails.ToListAsync());
        Assert.Equal(3, (await db.Products.AsNoTracking().SingleAsync()).StockQuantity);
        _app.Gls.Unavailable = false;
        await request.WithQuoteAsync(client);
        using var recovered = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Created, recovered.StatusCode);
        var order = (await recovered.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal(replacement.Id, order.GlsDeliveryPointId);
        Assert.Equal(replacement.Address, order.GlsDeliveryPointAddress);
        Assert.Single(await db.Orders.ToListAsync());
        Assert.Equal(2, await db.OrderEmails.CountAsync());
        Assert.Equal(2, (await db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [SqlServerFact]
    public async Task DraftRestoresSelectionAndKeepsPersonalFieldsWhenPointBecomesUnavailable()
    {
        using var client = _app.Factory.CreateClient();
        var cart = (await client.GetFromJsonAsync<CartDto>("/api/cart"))!;
        var path = $"/api/checkout-drafts/{cart.Id}";
        var point = Point();
        var fields = new CheckoutDraftFields { CustomerName = "Draft customer", FulfillmentMethod = point.Type, GlsDeliveryPointId = point.Id, DeliveryStreet = "Untrusted address" };
        using var saved = await client.PutAsJsonAsync(path, new SaveCheckoutDraftDto(null, fields));
        saved.EnsureSuccessStatusCode();
        var restored = (await client.GetFromJsonAsync<CheckoutDraftDto>(path))!;
        Assert.Equal(point, restored.DeliveryPoint);
        Assert.Equal("", restored.Fields!.DeliveryStreet);
        _app.Gls.Points.Clear();
        var obsolete = (await client.GetFromJsonAsync<CheckoutDraftDto>(path))!;
        Assert.NotNull(obsolete.DeliveryPointError);
        Assert.Null(obsolete.DeliveryPoint);
        Assert.Equal("Draft customer", obsolete.Fields!.CustomerName);
        _app.Gls.Unavailable = true;
        var unavailable = (await client.GetFromJsonAsync<CheckoutDraftDto>(path))!;
        Assert.NotNull(unavailable.DeliveryPointError);
        Assert.Equal("Draft customer", unavailable.Fields!.CustomerName);
        fields.FulfillmentMethod = FulfillmentMethod.PersonalPickup;
        using var switched = await client.PutAsJsonAsync(path, new SaveCheckoutDraftDto(restored.Version, fields));
        switched.EnsureSuccessStatusCode();
        Assert.Null((await switched.Content.ReadFromJsonAsync<CheckoutDraftDto>())!.Fields!.GlsDeliveryPointId);
    }
}
