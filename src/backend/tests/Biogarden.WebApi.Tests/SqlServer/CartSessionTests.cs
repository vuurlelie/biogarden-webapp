using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class CartSessionTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task Merge_LastReservedItem_TransfersOnceWithoutConsumingStock()
    {
        var (userId, productId) = await SeedAsync(1);
        await WithCartAsync(service => service.AddItemAsync(null, "guest", new() { ProductId = productId, Quantity = 1 }));

        var merged = await WithCartAsync(service => service.MergeGuestCartIntoUserCartAsync(userId, "guest"));
        var repeated = await WithCartAsync(service => service.MergeGuestCartIntoUserCartAsync(userId, "guest"));

        Assert.Equal(1, merged.TotalItemCount);
        Assert.Equal(merged.Id, repeated.Id);
        Assert.Equal(1, repeated.TotalItemCount);
        await using var database = _app.OpenDatabase();
        Assert.Equal(1, (await database.Products.SingleAsync()).StockQuantity);
        Assert.True((await database.Carts.SingleAsync(cart => cart.SessionId == "guest")).IsCheckedOut);
        Assert.Equal(1, await database.CartItems.Where(item => !item.Cart!.IsCheckedOut).SumAsync(item => item.Quantity));
    }

    [SqlServerFact]
    public async Task Merge_OverlappingItems_PreservesAnotherCustomersReservation()
    {
        var (userId, productId) = await SeedAsync(3);
        await WithCartAsync(service => service.AddItemAsync(userId, null, new() { ProductId = productId, Quantity = 1 }));
        await WithCartAsync(service => service.AddItemAsync(null, "guest", new() { ProductId = productId, Quantity = 1 }));
        await WithCartAsync(service => service.AddItemAsync(null, "other", new() { ProductId = productId, Quantity = 1 }));

        var merged = await WithCartAsync(service => service.MergeGuestCartIntoUserCartAsync(userId, "guest"));

        Assert.Equal(2, Assert.Single(merged.Items).Quantity);
        await using var database = _app.OpenDatabase();
        Assert.Equal(1, (await database.Carts.Include(cart => cart.Items).SingleAsync(cart => cart.SessionId == "other")).Items.Single().Quantity);
        Assert.Equal(3, await database.CartItems.Where(item => !item.Cart!.IsCheckedOut).SumAsync(item => item.Quantity));
    }

    [SqlServerFact]
    public async Task Merge_StockReducedAfterReservation_LeavesBothCartsRecoverable()
    {
        var (userId, productId) = await SeedAsync(4);
        await WithCartAsync(service => service.AddItemAsync(userId, null, new() { ProductId = productId, Quantity = 1 }));
        await WithCartAsync(service => service.AddItemAsync(null, "guest", new() { ProductId = productId, Quantity = 2 }));
        await WithCartAsync(service => service.AddItemAsync(null, "other", new() { ProductId = productId, Quantity = 1 }));
        await using (var database = _app.OpenDatabase())
        {
            (await database.Products.SingleAsync()).StockQuantity = 3;
            await database.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => WithCartAsync(service => service.MergeGuestCartIntoUserCartAsync(userId, "guest")));

        await using var verification = _app.OpenDatabase();
        var carts = await verification.Carts.Include(cart => cart.Items).ToListAsync();
        Assert.All(carts, cart => Assert.False(cart.IsCheckedOut));
        Assert.Equal(1, carts.Single(cart => cart.UserId == userId).Items.Single().Quantity);
        Assert.Equal(2, carts.Single(cart => cart.SessionId == "guest").Items.Single().Quantity);
    }

    [SqlServerFact]
    public async Task Merge_OneOfSeveralProductsUnavailable_DoesNotPersistPartialChanges()
    {
        var (userId, productId) = await SeedAsync(5);
        int unavailableId;
        await using (var database = _app.OpenDatabase())
        {
            var product = new Product { Name = "Second product", Available = true, Price = 700m, StockQuantity = 5 };
            database.Products.Add(product);
            await database.SaveChangesAsync();
            unavailableId = product.Id;
        }
        await WithCartAsync(service => service.AddItemAsync(userId, null, new() { ProductId = productId, Quantity = 1 }));
        await WithCartAsync(service => service.AddItemAsync(null, "guest", new() { ProductId = productId, Quantity = 1 }));
        await WithCartAsync(service => service.AddItemAsync(null, "guest", new() { ProductId = unavailableId, Quantity = 1 }));
        await using (var database = _app.OpenDatabase())
        {
            (await database.Products.SingleAsync(product => product.Id == unavailableId)).Available = false;
            await database.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => WithCartAsync(service => service.MergeGuestCartIntoUserCartAsync(userId, "guest")));

        await using var verification = _app.OpenDatabase();
        var carts = await verification.Carts.Include(cart => cart.Items).ToListAsync();
        Assert.All(carts, cart => Assert.False(cart.IsCheckedOut));
        Assert.Equal(1, Assert.Single(carts.Single(cart => cart.UserId == userId).Items).Quantity);
        Assert.Equal(2, carts.Single(cart => cart.SessionId == "guest").Items.Count);
        Assert.All(carts.SelectMany(cart => cart.Items), item => Assert.Equal(1, item.Quantity));
    }

    private async Task<(Guid UserId, int ProductId)> SeedAsync(int stock)
    {
        await using var database = _app.OpenDatabase();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "cart-test@example.com", Email = "cart-test@example.com" };
        var product = new Product { Name = "Cart test product", Available = true, Price = 900m, StockQuantity = stock };
        database.Users.Add(user);
        database.Products.Add(product);
        await database.SaveChangesAsync();
        return (user.Id, product.Id);
    }

    private async Task<CartDto> WithCartAsync(Func<ICartService, Task<CartDto>> operation)
    {
        using var scope = _app.Factory.Services.CreateScope();
        return await operation(scope.ServiceProvider.GetRequiredService<ICartService>());
    }
}
