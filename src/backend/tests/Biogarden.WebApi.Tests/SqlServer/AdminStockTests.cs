using System.Net.Http.Json;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.DTOs.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class AdminStockTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothEditPathsRejectStockBelowReservationsAndAllowTheirExactQuantity(bool inventory)
    {
        using var client = _app.Factory.CreateClient();
        await using var db = _app.OpenDatabase();
        var product = new Product { Name = "Reserved tea", Price = 1000, StockQuantity = 5, Available = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        (await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = product.Id, Quantity = 3 })).EnsureSuccessStatusCode();
        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateAsync(product.Id, 2, inventory));
        Assert.Equal(5, (await db.Products.AsNoTracking().SingleAsync()).StockQuantity);
        await UpdateAsync(product.Id, 3, inventory);
        Assert.Equal(3, (await db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [SqlServerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReservationAddedAfterStockReadCannotBeOverwritten(bool inventory)
    {
        using var client = _app.Factory.CreateClient();
        await using var db = _app.OpenDatabase();
        var product = new Product { Name = "Concurrent tea", Price = 1000, StockQuantity = 5, Available = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        // Preload the admin's tracked entity, then let a real cart mutation advance its rowversion.
        using var scope = _app.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Biogarden.DataAccess.Abstractions.IUnitOfWork>().ProductRepository.GetByIdAsync(product.Id);
        (await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = product.Id, Quantity = 3 })).EnsureSuccessStatusCode();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => UpdateAsync(product.Id, 4, inventory, scope.ServiceProvider));
        Assert.Equal(5, (await db.Products.AsNoTracking().SingleAsync()).StockQuantity);
        Assert.Equal(3, await db.CartItems.SumAsync(x => x.Quantity));
    }

    private async Task UpdateAsync(int id, int quantity, bool inventory, IServiceProvider? provider = null)
    {
        using var scope = provider is null ? _app.Factory.Services.CreateScope() : null;
        provider ??= scope!.ServiceProvider;
        var product = await provider.GetRequiredService<Biogarden.DataAccess.Abstractions.IUnitOfWork>().ProductRepository.GetByIdAsync(id);
        var version = Convert.ToBase64String(product!.RowVersion);
        if (inventory)
            await provider.GetRequiredService<IAdminInventoryService>().UpdateAsync(id, new UpdateAdminInventoryDto { Version = version, StockQuantity = quantity, Available = true });
        else
            await provider.GetRequiredService<IProductService>().UpdateAsync(id, new ProductEditDto { Version = version, Name = "Reserved tea", Price = 1000, StockQuantity = quantity, Available = true });
    }
}
