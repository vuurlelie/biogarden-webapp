using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Payments;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class PaymentRecoveryTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    private readonly Provider _provider = new();
    private readonly BindingFailure _failure = new();
    private WebApplicationFactory<Program> _api = null!;
    private Guid _orderId;

    public async Task InitializeAsync()
    {
        await _app.InitializeAsync();
        _api = _app.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOptions<SumUpOptions>>();
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new SumUpOptions { Enabled = true, MerchantCode = "merchant", ApiKey = "fake" }));
            services.RemoveAll<ISumUpCheckoutClient>();
            services.AddSingleton<ISumUpCheckoutClient>(_provider);
            services.AddDbContext<AppDbContext>(options => options.AddInterceptors(_failure));
        }));
        await using var db = _app.OpenDatabase();
        var cart = new Cart { Id = Guid.NewGuid(), SessionId = "guest", IsCheckedOut = true };
        var product = new Product { Name = "Payment tea", Price = 900, Available = true, StockQuantity = 2 };
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "PAYMENT-TEST",
            Cart = cart,
            CartId = cart.Id,
            TotalPrice = 900,
            Currency = "HUF",
            PaymentMethod = OrderPaymentMethod.OnlineCard
        };
        order.Items.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            Product = product,
            ProductName = product.Name,
            Quantity = 1,
            UnitPrice = 900,
            LineTotal = 900
        });
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        _orderId = order.Id;
    }

    public async Task DisposeAsync()
    {
        if (_api is not null) await _api.DisposeAsync();
        await _app.DisposeAsync();
    }

    private async Task<string> StartAsync(string session = "guest")
    {
        await using var scope = _api.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IOrderService>()
            .CreateSumUpCheckoutAsync(null, session, _orderId)).CheckoutId;
    }

    [SqlServerFact]
    public async Task ParallelRetriesCreateOneCheckoutAndReuseItAfterNewRequest()
    {
        _provider.BlockCreate = true;
        var first = StartAsync();
        await _provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var second = StartAsync();
        _provider.Release.TrySetResult();
        var ids = await Task.WhenAll(first, second);
        Assert.Equal(ids[0], ids[1]);
        Assert.Equal(ids[0], await StartAsync());
        Assert.Equal(1, _provider.Creates);
        await using var db = _app.OpenDatabase();
        Assert.Single(await db.PaymentAttempts.ToListAsync());
    }

    [SqlServerTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProviderSuccessWithLostResponseOrLocalSaveFailureRecoversByReference(bool lostResponse)
    {
        _provider.LoseResponse = lostResponse;
        _failure.Enabled = !lostResponse;
        await Assert.ThrowsAnyAsync<Exception>(() => StartAsync());
        await using (var db = _app.OpenDatabase())
        {
            Assert.Null((await db.PaymentAttempts.SingleAsync()).CheckoutId);
            Assert.Null((await db.Orders.SingleAsync()).SumUpCheckoutId);
        }
        Assert.Equal("checkout-1", await StartAsync());
        Assert.Equal(1, _provider.Creates);
        Assert.Equal(1, _provider.Lookups);
    }

    [SqlServerFact]
    public async Task UncertainMissingCheckoutDoesNotBlindlyCreateAgain()
    {
        _provider.LoseResponse = true;
        await Assert.ThrowsAsync<PaymentRecoveryException>(() => StartAsync());
        _provider.Checkouts.Clear();
        await Assert.ThrowsAsync<PaymentRecoveryException>(() => StartAsync());
        Assert.Equal(1, _provider.Creates);
    }

    [SqlServerFact]
    public async Task ExpiredCheckoutIsRetainedWhenANewAttemptIsCreated()
    {
        Assert.Equal("checkout-1", await StartAsync());
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "EXPIRED" };
        Assert.Equal("checkout-2", await StartAsync());
        await using var db = _app.OpenDatabase();
        var attempts = await db.PaymentAttempts.ToListAsync();
        Assert.Equal(2, attempts.Count);
        Assert.Contains(attempts, x => x.CheckoutId == "checkout-1" && x.Status == "EXPIRED");
        Assert.Contains(attempts, x => x.CheckoutId == "checkout-2");
    }

    [SqlServerFact]
    public async Task OtherGuestCannotStartOrRecoverPayment()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => StartAsync("stranger"));
        Assert.Equal(0, _provider.Creates);
        await using var db = _app.OpenDatabase();
        Assert.Empty(await db.PaymentAttempts.ToListAsync());
    }

    private sealed class BindingFailure : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && data.Context!.ChangeTracker.Entries<PaymentAttempt>().Any(x => x.Entity.CheckoutId is not null))
            {
                Enabled = false;
                throw new InvalidOperationException("Simulated payment binding failure");
            }
            return base.SavingChangesAsync(data, result, cancellationToken);
        }
    }

    private sealed class Provider : ISumUpCheckoutClient
    {
        public List<SumUpCheckoutResult> Checkouts { get; } = [];
        public int Creates { get; private set; }
        public int Lookups { get; private set; }
        public bool LoseResponse { get; set; }
        public bool BlockCreate { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<SumUpCheckoutResult> CreateAsync(SumUpCreateCheckoutRequest request, string key, Uri url, CancellationToken token = default)
        {
            Creates++;
            var result = new SumUpCheckoutResult($"checkout-{Creates}", "PENDING", "https://checkout.sumup.com/test", null,
                request.Amount, request.Currency, request.CheckoutReference, request.MerchantCode);
            Checkouts.Add(result);
            Entered.TrySetResult();
            if (BlockCreate) await Release.Task.WaitAsync(token);
            if (LoseResponse) { LoseResponse = false; throw new HttpRequestException("Simulated lost response"); }
            return result;
        }
        public Task<SumUpCheckoutResult> GetAsync(string id, string key, Uri url, CancellationToken token = default)
            => Task.FromResult(Checkouts.Single(x => x.CheckoutId == id));
        public Task<IReadOnlyList<SumUpCheckoutResult>> FindAsync(string reference, string key, Uri url, CancellationToken token = default)
        {
            Lookups++;
            return Task.FromResult<IReadOnlyList<SumUpCheckoutResult>>(Checkouts.Where(x => x.CheckoutReference == reference).ToArray());
        }
    }
}
