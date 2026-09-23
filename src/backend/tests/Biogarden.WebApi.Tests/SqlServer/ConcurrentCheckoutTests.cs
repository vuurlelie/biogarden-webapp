using System.Net;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.Constants;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class ConcurrentCheckoutTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task LastItem_TwoCustomersCompetingToReserveAndCheckout_OnlyOneCanBuy()
    {
        var productId = await SeedProductAsync(stock: 1);
        using var first = _app.Factory.CreateClient();
        using var second = _app.Factory.CreateClient();
        // Establish separate session cookies and empty carts before the competing actions.
        await InitializeCartAsync(first);
        await InitializeCartAsync(second);
        _app.SaveGate.EntityType = typeof(CartItem);

        var firstPurchase = ReserveAndBuyAsync(first, productId);
        var secondPurchase = ReserveAndBuyAsync(second, productId);
        var responses = await ReleaseTogetherAsync(firstPurchase, secondPurchase);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(2, _app.SaveGate.Arrivals);
        await using var database = _app.OpenDatabase();
        Assert.Equal(0, (await database.Products.SingleAsync()).StockQuantity);
        var order = await database.Orders.Include(item => item.Items).SingleAsync();
        Assert.Equal(1, Assert.Single(order.Items).Quantity);
        Assert.Equal(1, await database.Carts.CountAsync(cart => cart.IsCheckedOut));
        Assert.Equal(1, await database.CartItems.SumAsync(item => item.Quantity));
        Assert.Equal(2, await database.OrderEmails.CountAsync());
        Assert.Empty(_app.Sender.Sent);
    }

    [SqlServerFact]
    public async Task SameCheckout_SubmittedConcurrently_PersistsOnceAndRetryReturnsOriginalOrder()
    {
        var productId = await SeedProductAsync(stock: 1);
        using var client = _app.Factory.CreateClient();
        using var added = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = productId, Quantity = 1 });
        added.EnsureSuccessStatusCode();
        var request = Request();
        _app.SaveGate.EntityType = typeof(Order);

        var first = client.PostAsJsonAsync("/api/orders", request);
        var second = client.PostAsJsonAsync("/api/orders", request);
        var responses = await ReleaseTogetherAsync(first, second);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        var created = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        var competing = Assert.Single(responses, response => response != created);
        // The competing transaction may recover the persisted order or reject a stale rowversion.
        Assert.Contains(competing.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        var order = (await created.Content.ReadFromJsonAsync<OrderDto>())!;
        if (competing.StatusCode == HttpStatusCode.OK)
            Assert.Equal(order.Id, (await competing.Content.ReadFromJsonAsync<OrderDto>())!.Id);

        using var retry = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(order.Id, (await retry.Content.ReadFromJsonAsync<OrderDto>())!.Id);
        Assert.Equal(2, _app.SaveGate.Arrivals);
        await using var database = _app.OpenDatabase();
        Assert.Equal(order.Id, (await database.Orders.SingleAsync()).Id);
        Assert.Equal(0, (await database.Products.SingleAsync()).StockQuantity);
        Assert.Equal(1, await database.OrderItems.CountAsync());
        Assert.True((await database.Carts.SingleAsync()).IsCheckedOut);
        Assert.Equal(2, await database.OrderEmails.CountAsync());
        Assert.Equal(2, await database.OrderEmails.Select(email => email.Kind).Distinct().CountAsync());
        Assert.Empty(_app.Sender.Sent);
    }

    [SqlServerFact]
    public async Task QuantityIncreases_InTwoCarts_CannotReserveMoreThanAvailableStock()
    {
        var productId = await SeedProductAsync(stock: 3);
        using var first = _app.Factory.CreateClient();
        using var second = _app.Factory.CreateClient();
        async Task<Guid> AddAsync(HttpClient client)
        {
            using var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = productId, Quantity = 1 });
            response.EnsureSuccessStatusCode();
            return Assert.Single((await response.Content.ReadFromJsonAsync<CartDto>())!.Items).Id;
        }
        var firstItem = await AddAsync(first);
        var secondItem = await AddAsync(second);
        _app.SaveGate.EntityType = typeof(CartItem);
        _app.SaveGate.State = EntityState.Modified;
        var responses = await ReleaseTogetherAsync(
            first.PutAsJsonAsync($"/api/cart/items/{firstItem}", new UpdateCartItemQuantityDto { Quantity = 2 }),
            second.PutAsJsonAsync($"/api/cart/items/{secondItem}", new UpdateCartItemQuantityDto { Quantity = 2 }));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(2, _app.SaveGate.Arrivals);
        await using var database = _app.OpenDatabase();
        Assert.Equal(new[] { 1, 2 }, await database.CartItems.OrderBy(item => item.Quantity).Select(item => item.Quantity).ToArrayAsync());
        Assert.Equal(3, (await database.Products.SingleAsync()).StockQuantity);
        Assert.Empty(await database.Orders.ToListAsync());
        Assert.Empty(await database.OrderEmails.ToListAsync());
    }

    private async Task<HttpResponseMessage[]> ReleaseTogetherAsync(params Task<HttpResponseMessage>[] requests)
    {
        HttpResponseMessage[] responses;
        var bothArrived = false;
        try
        {
            await _app.SaveGate.WaitForBothAsync();
            bothArrived = true;
        }
        finally
        {
            _app.SaveGate.Release();
            // Drain requests before the fixture tears down its database, even if the gate timed out.
            responses = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(40));
            if (!bothArrived)
                foreach (var response in responses) response.Dispose();
        }
        return responses;
    }

    private static async Task<HttpResponseMessage> ReserveAndBuyAsync(HttpClient client, int productId)
    {
        var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = productId, Quantity = 1 });
        if (!response.IsSuccessStatusCode) return response;
        response.Dispose();
        return await client.PostAsJsonAsync("/api/orders", Request());
    }

    private static async Task InitializeCartAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/cart");
        response.EnsureSuccessStatusCode();
    }

    private async Task<int> SeedProductAsync(int stock)
    {
        await using var database = _app.OpenDatabase();
        var product = new Product { Name = "Last item", Available = true, Price = 900m, StockQuantity = stock };
        database.Products.Add(product);
        await database.SaveChangesAsync();
        return product.Id;
    }

    private static PlaceOrderDto Request() => new()
    {
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        CustomerName = "Concurrent customer",
        CustomerEmail = "customer@example.com",
        CustomerPhone = "+361234567",
        FulfillmentMethod = FulfillmentMethod.PersonalPickup,
        AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
        AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion
    };
}
