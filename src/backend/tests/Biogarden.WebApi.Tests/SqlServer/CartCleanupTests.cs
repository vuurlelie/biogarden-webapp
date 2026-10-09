using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Biogarden.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Biogarden.DataAccess.Abstractions;
using Biogarden.WebApi.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class CartCleanupTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    private readonly Clock _clock = new(new DateTimeOffset(2040, 1, 1, 12, 0, 0, TimeSpan.Zero));
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task Cleanup_IsBoundedRepeatableAndPreservesActiveRecentCheckedOutAndOrderedCarts()
    {
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-7);
        var eligible = new[] { NewCart(cutoff.AddDays(-2)), NewCart(cutoff.AddDays(-1)), NewCart(cutoff) };
        var active = NewCart(_clock.GetUtcNow().UtcDateTime.AddMinutes(20));
        var recent = NewCart(cutoff.AddTicks(1));
        var checkedOut = NewCart(cutoff.AddDays(-3));
        checkedOut.IsCheckedOut = true;
        var pendingCart = NewCart(cutoff.AddDays(-3));
        var paidCart = NewCart(cutoff.AddDays(-3));
        var pending = new Order { Id = Guid.NewGuid(), OrderNumber = "cleanup-pending", Cart = pendingCart, TotalPrice = 900m };
        var paid = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "cleanup-paid",
            Cart = paidCart,
            TotalPrice = 900m,
            PaymentStatus = PaymentStatus.Paid,
            StockCommitted = true
        };
        await using (var seed = _app.OpenDatabase())
        {
            var product = new Product { Name = "Retained stock", Available = true, StockQuantity = 9 };
            foreach (var cart in eligible.Append(active)) cart.Items.Add(new CartItem { Product = product, Quantity = 1 });
            eligible[0].CheckoutDraftJson = "test draft slated for deletion";
            seed.Carts.AddRange(eligible);
            seed.Carts.AddRange(active, recent, checkedOut);
            seed.Orders.AddRange(pending, paid);
            await seed.SaveChangesAsync();
        }

        Assert.Equal(2, await RunAsync(2));
        await using (var first = _app.OpenDatabase())
        {
            Assert.False(await first.Carts.AnyAsync(cart => cart.Id == eligible[0].Id || cart.Id == eligible[1].Id));
            Assert.True(await first.Carts.AnyAsync(cart => cart.Id == eligible[2].Id));
            Assert.False(await first.CartItems.AnyAsync(item => item.CartId == eligible[0].Id || item.CartId == eligible[1].Id));
        }
        Assert.Equal(1, await RunAsync(2));
        Assert.Equal(0, await RunAsync(2));
        await using var verification = _app.OpenDatabase();
        var remaining = await verification.Carts.Select(cart => cart.Id).ToListAsync();
        Assert.Equal(5, remaining.Count);
        Assert.All(new[] { active, recent, checkedOut, pendingCart, paidCart }, cart => Assert.Contains(cart.Id, remaining));
        Assert.Equal(9, (await verification.Products.SingleAsync()).StockQuantity);
        var orders = await verification.Orders.OrderBy(order => order.OrderNumber).ToListAsync();
        Assert.Equal(2, orders.Count);
        Assert.All(orders, order => Assert.Equal(OrderStatus.Pending, order.Status));
        Assert.Equal(PaymentStatus.Pending, orders.Single(order => order.Id == pending.Id).PaymentStatus);
        Assert.True(orders.Single(order => order.Id == paid.Id).StockCommitted);
        Assert.Equal(PaymentStatus.Paid, orders.Single(order => order.Id == paid.Id).PaymentStatus);
    }

    [SqlServerFact]
    public async Task Cleanup_RechecksCurrentStateInsteadOfDeletingAStaleTrackedSnapshot()
    {
        await using var staleContext = _app.OpenDatabase();
        var cart = NewCart(_clock.GetUtcNow().UtcDateTime.AddDays(-10));
        staleContext.Carts.Add(cart);
        await staleContext.SaveChangesAsync();
        await using (var changed = _app.OpenDatabase())
        {
            var current = await changed.Carts.SingleAsync();
            current.ExpiresAtUtc = _clock.GetUtcNow().UtcDateTime.AddMinutes(30);
            await changed.SaveChangesAsync();
        }
        var repository = new CartRepository(staleContext, _clock);
        Assert.Equal(0, await repository.DeleteExpiredBatchAsync(_clock.GetUtcNow().UtcDateTime.AddDays(-7), 10));
        Assert.True(await staleContext.Carts.AsNoTracking().AnyAsync(item => item.Id == cart.Id));
    }

    [SqlServerFact]
    public async Task Cancellation_LeavesEligibleCartsForTheNextBatch()
    {
        await using var database = _app.OpenDatabase();
        database.Carts.Add(NewCart(_clock.GetUtcNow().UtcDateTime.AddDays(-10)));
        await database.SaveChangesAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var repository = new CartRepository(database, _clock);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.DeleteExpiredBatchAsync(_clock.GetUtcNow().UtcDateTime.AddDays(-7), 10, cancellation.Token));
        Assert.Equal(1, await database.Carts.CountAsync());
        Assert.Equal(1, await RunAsync(10));
    }

    [SqlServerFact]
    public async Task Repository_RejectsUnboundedBatchesAndNonExpiredCutoffs()
    {
        await using var database = _app.OpenDatabase();
        var repository = new CartRepository(database, _clock);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.DeleteExpiredBatchAsync(_clock.GetUtcNow().UtcDateTime, 100));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.DeleteExpiredBatchAsync(_clock.GetUtcNow().UtcDateTime.AddDays(-7), 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.DeleteExpiredBatchAsync(_clock.GetUtcNow().UtcDateTime.AddDays(-7), 1001));
    }

    [SqlServerFact]
    public async Task Worker_ProcessesOneBoundedStartupBatchAndStopsDuringItsInterval()
    {
        await using (var seed = _app.OpenDatabase())
        {
            seed.Carts.AddRange(Enumerable.Range(0, 5).Select(_ => NewCart(_clock.GetUtcNow().UtcDateTime.AddDays(-10))));
            await seed.SaveChangesAsync();
        }
        var services = new ServiceCollection();
        services.AddScoped<IAppDbContext>(_ => _app.OpenDatabase());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<TimeProvider>(_clock);
        services.AddScoped<CartCleanupService>();
        // Supply the same immutable settings to both the worker and its scoped service.
        var options = Microsoft.Extensions.Options.Options.Create(new CartCleanupOptions { BatchSize = 2 });
        services.AddSingleton<Microsoft.Extensions.Options.IOptions<CartCleanupOptions>>(options);
        await using var provider = services.BuildServiceProvider();
        var log = new CompletionLog();
        using var worker = new CartCleanupWorker(provider.GetRequiredService<IServiceScopeFactory>(), options, _clock, log);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            var message = await log.FirstEntry.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Contains("2 deleted", message);
            Assert.Contains("retention 7 days", message);
        }
        finally
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await worker.StopAsync(timeout.Token);
        }
        await using var verification = _app.OpenDatabase();
        Assert.Equal(3, await verification.Carts.CountAsync());
    }

    private sealed class CompletionLog : ILogger<CartCleanupWorker>
    {
        public TaskCompletionSource<string> FirstEntry { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => FirstEntry.TrySetResult(formatter(state, exception));
    }

    private async Task<int> RunAsync(int batchSize)
    {
        await using var database = _app.OpenDatabase();
        var service = new CartCleanupService(new UnitOfWork(database, _clock), _clock,
            Microsoft.Extensions.Options.Options.Create(new CartCleanupOptions { BatchSize = batchSize }));
        return await service.RunBatchAsync();
    }

    private static Cart NewCart(DateTime expires) => new()
    { Id = Guid.NewGuid(), SessionId = Guid.NewGuid().ToString(), ExpiresAtUtc = expires };

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

