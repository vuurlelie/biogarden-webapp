using System.Net;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class CartQuantityBoundsTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task QuantityBoundary_IsAccepted_OverflowIsBadRequestAndPreservesReservation()
    {
        var product = new Product { Name = "Quantity boundary", Available = true, StockQuantity = int.MaxValue, Price = 1 };
        await using (var seed = _app.OpenDatabase())
        {
            seed.Products.Add(product);
            await seed.SaveChangesAsync();
        }
        using var client = _app.Factory.CreateClient();
        using var added = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = product.Id, Quantity = 1 });
        added.EnsureSuccessStatusCode();
        using var boundary = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = product.Id, Quantity = int.MaxValue - 1 });
        boundary.EnsureSuccessStatusCode();
        var cart = (await boundary.Content.ReadFromJsonAsync<CartDto>())!;
        Assert.Equal(int.MaxValue, Assert.Single(cart.Items).Quantity);
        using var overflow = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = product.Id, Quantity = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, overflow.StatusCode);
        await using var check = _app.OpenDatabase();
        Assert.Equal(int.MaxValue, (await check.CartItems.SingleAsync()).Quantity);
        Assert.Equal(cart.ExpiresAtUtc, (await check.Carts.SingleAsync()).ExpiresAtUtc);
        Assert.Equal(int.MaxValue, (await check.Products.SingleAsync()).StockQuantity);
    }
}
