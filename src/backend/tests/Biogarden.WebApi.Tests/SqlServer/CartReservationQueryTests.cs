using System.Data.Common;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class CartReservationQueryTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2040, 1, 1, 12, 0, 0, TimeSpan.Zero));
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerTheory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task LookupAndReservation_UseTheSameInjectedExpiryBoundary(int expiryTicks, bool active)
    {
        await using var database = _app.OpenDatabase();
        var product = new Product { Name = "Boundary product", Available = true, StockQuantity = 20 };
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "boundary@example.com" };
        var guest = CartWith(product, 2, _clock.GetUtcNow().UtcDateTime.AddTicks(expiryTicks));
        var owned = CartWith(product, 3, guest.ExpiresAtUtc);
        owned.User = user;
        owned.SessionId = null;
        database.Carts.AddRange(guest, owned);
        await database.SaveChangesAsync();
        var repository = new CartRepository(database, _clock);

        Assert.Equal(active, await repository.GetActiveBySessionIdAsync(guest.SessionId!)is not null);
        Assert.Equal(active, await repository.GetActiveByUserIdAsync(user.Id)is not null);
        Assert.Equal(active ? 5 : 0, await repository.GetActiveReservedQuantityForProductAsync(product.Id));
        var grouped = await repository.GetActiveReservedQuantitiesAsync([product.Id]);
        Assert.Equal(active ? 5 : 0, grouped.GetValueOrDefault(product.Id));
    }

    [SqlServerFact]
    public async Task GroupedReservations_UseOneQueryAndExcludeBothMergeOwnersExpiredAndCheckedOutCarts()
    {
        await using var seed = _app.OpenDatabase();
        var first = new Product { Name = "First", Available = true, StockQuantity = 100 };
        var second = new Product { Name = "Second", Available = true, StockQuantity = 100 };
        var expires = _clock.GetUtcNow().UtcDateTime.AddMinutes(10);
        var destination = CartWith(first, 11, expires);
        var source = CartWith(first, 13, expires);
        var competing = CartWith(first, 2, expires);
        competing.Items.Add(new CartItem { Product = second, Quantity = 3 });
        var another = CartWith(first, 5, expires);
        var expired = CartWith(first, 17, _clock.GetUtcNow().UtcDateTime);
        var checkedOut = CartWith(first, 19, expires);
        checkedOut.IsCheckedOut = true;
        seed.Carts.AddRange(destination, source, competing, another, expired, checkedOut);
        await seed.SaveChangesAsync();

        var counter = new QueryCounter();
        await using var database = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(seed.Database.GetConnectionString()).AddInterceptors(counter).Options);
        var repository = new CartRepository(database, _clock);
        Assert.Empty(await repository.GetActiveReservedQuantitiesAsync([]));
        Assert.Equal(0, counter.Reads);
        var quantities = await repository.GetActiveReservedQuantitiesAsync([first.Id, second.Id, first.Id, int.MaxValue], destination.Id, source.Id);

        Assert.Equal(1, counter.Reads);
        Assert.Equal(2, quantities.Count);
        Assert.Equal(7, quantities[first.Id]);
        Assert.Equal(3, quantities[second.Id]);
        Assert.Equal(quantities[first.Id], await repository.GetActiveReservedQuantityForProductAsync(first.Id, destination.Id, source.Id));
    }

    [SqlServerFact]
    public async Task ServiceAndRepository_ShareClockWhenReplacingAnExpiredCart()
    {
        await using var database = _app.OpenDatabase();
        var old = new Cart { Id = Guid.NewGuid(), SessionId = "replacement", ExpiresAtUtc = _clock.GetUtcNow().UtcDateTime };
        database.Carts.Add(old);
        await database.SaveChangesAsync();
        var service = new CartService(new UnitOfWork(database, _clock), _clock);

        var replacement = await service.GetCartAsync(null, "replacement");
        Assert.NotEqual(old.Id, replacement.Id);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddMinutes(30), replacement.ExpiresAtUtc);
        var repeated = await service.GetCartAsync(null, "replacement");
        Assert.Equal(replacement.Id, repeated.Id);
        Assert.Equal(2, await database.Carts.CountAsync());
    }

    private static Cart CartWith(Product product, int quantity, DateTime expires) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid().ToString(),
        ExpiresAtUtc = expires,
        Items = [new CartItem { Product = product, Quantity = quantity }]
    };

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Reads { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Reads++;
            return ValueTask.FromResult(result);
        }
    }
}
