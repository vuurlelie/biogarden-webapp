using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class UnconfirmedAccountCleanupTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    private readonly Clock _clock = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task Cleanup_IsBoundedAndPreservesConfirmedRecentProtectedAndReferencedData()
    {
        var expired = Enumerable.Range(0, 3).Select(_ => NewUser()).ToArray();
        var confirmed = NewUser();
        confirmed.EmailConfirmed = true;
        var boundary = NewUser();
        boundary.CreatedAt = _clock.GetUtcNow().UtcDateTime.AddDays(-14);
        var deactivated = NewUser();
        deactivated.DeactivatedAtUtc = _clock.GetUtcNow().UtcDateTime;
        var anonymized = NewUser();
        anonymized.AnonymizedAtUtc = _clock.GetUtcNow().UtcDateTime;
        var administrator = NewUser();
        var ordered = NewUser();
        var exclusive = NewAddress();
        var shared = NewAddress();
        var historical = NewAddress();
        expired[0].BillingAddress = exclusive;
        expired[1].BillingAddress = shared;
        confirmed.BillingAddress = shared;
        expired[2].ShippingAddress = historical;
        await using (var seed = _app.OpenDatabase())
        {
            seed.Users.AddRange(expired);
            seed.Users.AddRange(confirmed, boundary, deactivated, anonymized, administrator, ordered);
            var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Admin", NormalizedName = "ADMIN" };
            seed.Roles.Add(role);
            seed.UserRoles.Add(new IdentityUserRole<Guid> { RoleId = role.Id, UserId = administrator.Id });
            seed.Orders.Add(new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = "cleanup-history",
                UserId = ordered.Id,
                Cart = new Cart { Id = Guid.NewGuid(), SessionId = "historical", IsCheckedOut = true },
                Address = historical
            });
            seed.Carts.Add(new Cart { Id = Guid.NewGuid(), UserId = expired[0].Id });
            await seed.SaveChangesAsync();
        }
        Assert.Equal(0, await RunAsync(2, enabled: false));
        Assert.Equal(2, await RunAsync(2));
        Assert.Equal(1, await RunAsync(2));
        Assert.Equal(0, await RunAsync(2));
        await using var check = _app.OpenDatabase();
        Assert.Equal(6, await check.Users.CountAsync());
        Assert.False(await check.Addresses.AnyAsync(address => address.Id == exclusive.Id));
        Assert.True(await check.Addresses.AnyAsync(address => address.Id == shared.Id));
        Assert.True(await check.Addresses.AnyAsync(address => address.Id == historical.Id));
        Assert.False(await check.Carts.AnyAsync(cart => cart.UserId == expired[0].Id));
        Assert.Equal(1, await check.Orders.CountAsync());
        Assert.Equal(0, await check.NotificationEmails.CountAsync());
        Assert.Empty(_app.Sender.Sent);
    }

    [SqlServerFact]
    public async Task FailedIdentityDeletion_RollsBackCartAndAddressChanges_LogsAndRetries()
    {
        var user = NewUser();
        user.BillingAddress = NewAddress();
        await using (var seed = _app.OpenDatabase())
        {
            seed.Users.Add(user);
            seed.Carts.Add(new Cart { Id = Guid.NewGuid(), UserId = user.Id });
            await seed.SaveChangesAsync();
        }
        var failure = new DeleteFailure();
        using var api = _app.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<AppDbContext>(options => options.AddInterceptors(failure))));
        var log = new TestLog();
        using (var scope = api.Services.CreateScope())
        {
            Assert.Equal(0, await CreateService(scope.ServiceProvider, 2, true, log).RunBatchAsync());
        }
        Assert.Contains(log.Messages, message => message.Contains("will be retried"));
        Assert.DoesNotContain(log.Messages, message => message.Contains(user.Email!));
        await using (var check = _app.OpenDatabase())
        {
            Assert.Equal(1, await check.Users.CountAsync());
            Assert.Equal(1, await check.Carts.CountAsync());
            Assert.Equal(1, await check.Addresses.CountAsync());
        }
        Assert.Equal(1, await RunAsync(2));
        await using var deleted = _app.OpenDatabase();
        Assert.Equal(0, await deleted.Users.CountAsync());
        Assert.Equal(0, await deleted.Addresses.CountAsync());
    }

    [SqlServerFact]
    public async Task ConcurrentWorkers_DeleteEachAccountOnlyOnce()
    {
        await using (var seed = _app.OpenDatabase())
        {
            seed.Users.AddRange(Enumerable.Range(0, 4).Select(_ => NewUser()));
            await seed.SaveChangesAsync();
        }
        var results = await Task.WhenAll(RunAsync(4), RunAsync(4));
        Assert.Equal(4, results.Sum());
        await using var check = _app.OpenDatabase();
        Assert.Empty(await check.Users.ToListAsync());
    }

    private async Task<int> RunAsync(int batchSize, bool enabled = true)
    {
        using var scope = _app.Factory.Services.CreateScope();
        return await CreateService(scope.ServiceProvider, batchSize, enabled, new TestLog()).RunBatchAsync();
    }

    private UnconfirmedAccountCleanupService CreateService(IServiceProvider provider, int batchSize, bool enabled, TestLog log) => new(
        provider.GetRequiredService<IAppDbContext>(), provider.GetRequiredService<UserManager<ApplicationUser>>(), _clock,
        Microsoft.Extensions.Options.Options.Create(new UnconfirmedAccountCleanupOptions { BatchSize = batchSize, Enabled = enabled }),
        Microsoft.Extensions.Options.Options.Create(new AuthEmailOptions { UnconfirmedAccountRetentionDays = 14 }), log);

    private ApplicationUser NewUser()
    {
        var email = $"cleanup-{Guid.NewGuid():N}@example.test";
        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreatedAt = _clock.GetUtcNow().UtcDateTime.AddDays(-15)
        };
    }

    private static Address NewAddress() => new() { PostalCode = "2112", City = "Veresegyház", Street = "Teszt", HouseNumber = "1" };
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2040, 1, 1, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class DeleteFailure : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<ApplicationUser>().Any(entry => entry.State == EntityState.Deleted))
                throw new DbUpdateConcurrencyException("Simulated Identity concurrency failure.");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestLog : ILogger<UnconfirmedAccountCleanupService>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
