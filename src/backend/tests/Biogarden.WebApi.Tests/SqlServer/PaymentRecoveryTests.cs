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

    [SqlServerFact]
    public async Task RecoveryBySubmissionKeyRequiresOwningSessionAndHasNoCheckoutSideEffects()
    {
        var key = Guid.NewGuid().ToString("N");
        await using (var db = _app.OpenDatabase())
            await db.Orders.ExecuteUpdateAsync(set => set.SetProperty(x => x.IdempotencyKey, key));
        using var owner = _api.CreateClient();
        owner.DefaultRequestHeaders.Add("Cookie", "biogarden_cart_id=guest");
        using var response = await owner.GetAsync($"/api/orders/checkout-recovery/{key}");
        response.EnsureSuccessStatusCode();
        Assert.Contains("PAYMENT-TEST", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var stranger = _api.CreateClient();
        using var denied = await stranger.GetAsync($"/api/orders/checkout-recovery/{key}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, denied.StatusCode);
        Assert.DoesNotContain("PAYMENT-TEST", await denied.Content.ReadAsStringAsync());
        using var unknown = await owner.GetAsync($"/api/orders/checkout-recovery/{Guid.NewGuid():N}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, unknown.StatusCode);
        using var malformed = await owner.GetAsync("/api/orders/checkout-recovery/short");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, malformed.StatusCode);
        await using var verified = _app.OpenDatabase();
        Assert.Single(await verified.Orders.ToListAsync());
        Assert.Empty(await verified.PaymentAttempts.ToListAsync());
        Assert.Equal(0, _provider.Creates);
    }

    private sealed class BindingFailure : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public bool FailPaidOrder { get; set; }
        public bool FailProcessedEvent { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (FailPaidOrder && data.Context!.ChangeTracker.Entries<Order>().Any(x => x.State == EntityState.Modified && x.Entity.PaymentStatus == PaymentStatus.Paid))
            {
                FailPaidOrder = false;
                throw new InvalidOperationException("Simulated paid order save failure");
            }
            if (FailProcessedEvent && data.Context!.ChangeTracker.Entries<PaymentProviderEvent>().Any(x => x.Entity.ProcessedAtUtc.HasValue))
            {
                FailProcessedEvent = false;
                throw new InvalidOperationException("Simulated processed event save failure");
            }
            if (Enabled && data.Context!.ChangeTracker.Entries<PaymentAttempt>().Any(x => x.Entity.CheckoutId is not null))
            {
                Enabled = false;
                throw new InvalidOperationException("Simulated payment binding failure");
            }
            return base.SavingChangesAsync(data, result, cancellationToken);
        }
    }

    private async Task NotifyAsync(string checkoutId = "checkout-1")
    {
        await using var scope = _api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISumUpCheckoutStatusService>()
            .HandleAsync($$"""{"event_type":"CHECKOUT_STATUS_CHANGED","id":"{{checkoutId}}"}""");
    }

    [SqlServerFact]
    public async Task CallbackBindsAnAttemptAfterProviderResponseWasLost()
    {
        _provider.LoseResponse = true;
        await Assert.ThrowsAsync<PaymentRecoveryException>(() => StartAsync());
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "PAID", TransactionCode = "paid-1" };
        await NotifyAsync();
        await using var db = _app.OpenDatabase();
        Assert.Equal(PaymentStatus.Paid, (await db.Orders.SingleAsync()).PaymentStatus);
        Assert.Equal("checkout-1", (await db.PaymentAttempts.SingleAsync()).CheckoutId);
        Assert.NotNull((await db.PaymentProviderEvents.SingleAsync()).ProcessedAtUtc);
    }

    [SqlServerFact]
    public async Task DuplicateCallbackInsertRaceLeavesOneProcessedEvent()
    {
        await StartAsync();
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "PAID", TransactionCode = "paid-1" };
        _app.SaveGate.EntityType = typeof(PaymentProviderEvent);
        var first = NotifyAsync();
        var second = NotifyAsync();
        try { await _app.SaveGate.WaitForBothAsync(); }
        finally { _app.SaveGate.Release(); }
        await Task.WhenAll(first, second);
        await using var db = _app.OpenDatabase();
        var paymentEvent = Assert.Single(await db.PaymentProviderEvents.ToListAsync());
        Assert.NotNull(paymentEvent.ProcessedAtUtc);
        Assert.Null(paymentEvent.ProcessingError);
        Assert.Equal(PaymentStatus.Paid, (await db.Orders.SingleAsync()).PaymentStatus);
    }

    [SqlServerTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedSaveDoesNotFlushDirtyStateWhenRecordingDiagnosticsAndCanReplay(bool failOrder)
    {
        await StartAsync();
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "PAID", TransactionCode = "paid-1" };
        _failure.FailPaidOrder = failOrder;
        _failure.FailProcessedEvent = !failOrder;
        await Assert.ThrowsAsync<InvalidOperationException>(() => NotifyAsync());
        await using (var db = _app.OpenDatabase())
        {
            Assert.Equal(failOrder ? PaymentStatus.Pending : PaymentStatus.Paid, (await db.Orders.SingleAsync()).PaymentStatus);
            var paymentEvent = await db.PaymentProviderEvents.SingleAsync();
            Assert.Null(paymentEvent.ProcessedAtUtc);
            Assert.NotNull(paymentEvent.ProcessingFailedAtUtc);
        }
        await NotifyAsync();
        await using var verified = _app.OpenDatabase();
        Assert.Equal(PaymentStatus.Paid, (await verified.Orders.SingleAsync()).PaymentStatus);
        var recovered = await verified.PaymentProviderEvents.SingleAsync();
        Assert.NotNull(recovered.ProcessedAtUtc);
        Assert.Null(recovered.ProcessingError);
    }

    [SqlServerFact]
    public async Task DelayedFailureCannotOverwritePaidTransactionOrFailNewCheckout()
    {
        await StartAsync();
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "EXPIRED" };
        await StartAsync();
        await NotifyAsync();
        await using (var db = _app.OpenDatabase())
            Assert.Equal(PaymentStatus.Pending, (await db.Orders.SingleAsync()).PaymentStatus);
        _provider.Checkouts[1] = _provider.Checkouts[1] with { Status = "PAID", TransactionCode = "paid-2" };
        await NotifyAsync("checkout-2");
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "FAILED", TransactionCode = "failed-1" };
        await NotifyAsync();
        await using var verified = _app.OpenDatabase();
        var order = await verified.Orders.SingleAsync();
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal("paid-2", order.SumUpTransactionId);
    }

    [SqlServerFact]
    public async Task MismatchedMerchantCannotBindOrPayAnUncertainAttempt()
    {
        _provider.LoseResponse = true;
        await Assert.ThrowsAsync<PaymentRecoveryException>(() => StartAsync());
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "PAID", TransactionCode = "paid-1", MerchantCode = "other" };
        await Assert.ThrowsAsync<PaymentRecoveryException>(() => NotifyAsync());
        await using var db = _app.OpenDatabase();
        Assert.Equal(PaymentStatus.Pending, (await db.Orders.SingleAsync()).PaymentStatus);
        Assert.Null((await db.PaymentAttempts.SingleAsync()).CheckoutId);
        Assert.Null((await db.PaymentProviderEvents.SingleAsync()).ProcessedAtUtc);
    }

    [SqlServerFact]
    public async Task MalformedAndOversizedNotificationsAreRejectedBeforeProviderLookup()
    {
        using var client = _api.CreateClient();
        foreach (var payload in new[] { "{", "null", "{}", "{\"event_type\":\"CHECKOUT_STATUS_CHANGED\",\"id\":\"../oops\"}" })
        {
            using var response = await client.PostAsync("/api/sumup/checkout-status", new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var oversized = await client.PostAsync("/api/sumup/checkout-status", new StringContent(new string('x', 17000)));
        Assert.Equal(System.Net.HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        Assert.Equal(0, _provider.Gets);
    }

    [SqlServerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SecondPaidAttemptIsFlaggedWithoutReplacingOriginalTransaction(bool legacyMissingTransaction)
    {
        await StartAsync();
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "EXPIRED" };
        await StartAsync();
        _provider.Checkouts[1] = _provider.Checkouts[1] with { Status = "PAID", TransactionCode = "paid-2" };
        await NotifyAsync("checkout-2");
        if (legacyMissingTransaction)
        {
            await using var legacy = _app.OpenDatabase();
            await legacy.Orders.ExecuteUpdateAsync(set => set.SetProperty(x => x.SumUpTransactionId, (string?)null));
        }
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "PAID", TransactionCode = "paid-1" };
        await Assert.ThrowsAsync<PaymentRecoveryException>(() => NotifyAsync());
        await using var db = _app.OpenDatabase();
        var order = await db.Orders.SingleAsync();
        Assert.Equal(legacyMissingTransaction ? null : "paid-2", order.SumUpTransactionId);
        Assert.Equal("checkout-2", order.SumUpCheckoutId);
        Assert.NotNull((await db.PaymentProviderEvents.SingleAsync(x => x.CheckoutId == "checkout-1")).ProcessingFailedAtUtc);
    }

    [SqlServerFact]
    public async Task PaidCallbackAfterCancellationPreservesReleasedStockAndRequiresRefund()
    {
        await StartAsync();
        await using (var db = _app.OpenDatabase())
            await db.Orders.ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, OrderStatus.Cancelled));
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "PAID", TransactionCode = "paid-1" };
        await NotifyAsync();
        await using var verified = _app.OpenDatabase();
        var order = await verified.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(PaymentStatus.RefundPending, order.PaymentStatus);
        Assert.Equal(2, (await verified.Products.SingleAsync()).StockQuantity);
    }

    [SqlServerFact]
    public async Task PaidLabelWithoutSuccessfulTransactionCannotBeConfirmedByRetryOrWebhook()
    {
        await StartAsync();
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "PAID", TransactionCode = null };
        await Assert.ThrowsAsync<PaymentRecoveryException>(() => StartAsync());
        await Assert.ThrowsAsync<PaymentRecoveryException>(() => NotifyAsync());
        await using var db = _app.OpenDatabase();
        Assert.Equal(PaymentStatus.Pending, (await db.Orders.SingleAsync()).PaymentStatus);
        Assert.Null((await db.PaymentProviderEvents.SingleAsync()).ProcessedAtUtc);
        Assert.Equal(1, _provider.Creates);
    }

    [SqlServerFact]
    public async Task RetryingFailedCheckoutDoesNotEraseVerifiedFailureWhenNotificationIsDeduplicated()
    {
        await StartAsync();
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "FAILED" };
        await NotifyAsync();
        await StartAsync();
        await NotifyAsync();
        await using var db = _app.OpenDatabase();
        Assert.Equal(PaymentStatus.Failed, (await db.Orders.SingleAsync()).PaymentStatus);
        Assert.Single(await db.PaymentProviderEvents.ToListAsync());
        Assert.Equal(1, _provider.Creates);
    }

    [SqlServerFact]
    public async Task CancellationWhileProviderCreatesCheckoutCannotBeOverwrittenAndLaterPaymentRequiresRefund()
    {
        _provider.BlockCreate = true;
        var start = StartAsync();
        await _provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            await using var db = _app.OpenDatabase();
            await db.Orders.ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, OrderStatus.Cancelled));
        }
        finally { _provider.Release.TrySetResult(); }
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => start);
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "PAID", TransactionCode = "paid-1" };
        await NotifyAsync();
        await using var verified = _app.OpenDatabase();
        var order = await verified.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(PaymentStatus.RefundPending, order.PaymentStatus);
        Assert.Equal(2, (await verified.Products.SingleAsync()).StockQuantity);
        await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync());
        Assert.Equal(1, _provider.Creates);
    }

    [SqlServerFact]
    public async Task PaidCallbackAndCustomerRetryConvergeWithoutAnotherCheckout()
    {
        await StartAsync();
        _provider.Checkouts[0] = _provider.Checkouts[0] with { Status = "PAID", TransactionCode = "paid-1" };
        await Task.WhenAll(NotifyAsync(), Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync()));
        await using var db = _app.OpenDatabase();
        Assert.Equal(PaymentStatus.Paid, (await db.Orders.SingleAsync()).PaymentStatus);
        Assert.Equal(1, _provider.Creates);
    }

    [SqlServerTheory]
    [InlineData(0, "HUF")]
    [InlineData(900, "EUR")]
    public async Task InvalidLocalPaymentDataDoesNotCreateAnUncertainProviderIntent(int total, string currency)
    {
        await using (var db = _app.OpenDatabase())
            await db.Orders.ExecuteUpdateAsync(set => set.SetProperty(x => x.TotalPrice, total).SetProperty(x => x.Currency, currency));
        await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync());
        await using var verified = _app.OpenDatabase();
        Assert.Empty(await verified.PaymentAttempts.ToListAsync());
        Assert.Equal(0, _provider.Creates);
    }

    private sealed class Provider : ISumUpCheckoutClient
    {
        public List<SumUpCheckoutResult> Checkouts { get; } = [];
        public int Creates { get; private set; }
        public int Lookups { get; private set; }
        public int Gets { get; private set; }
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
        {
            Gets++;
            return Task.FromResult(Checkouts.Single(x => x.CheckoutId == id));
        }
        public Task<IReadOnlyList<SumUpCheckoutResult>> FindAsync(string reference, string key, Uri url, CancellationToken token = default)
        {
            Lookups++;
            return Task.FromResult<IReadOnlyList<SumUpCheckoutResult>>(Checkouts.Where(x => x.CheckoutReference == reference).ToArray());
        }
    }
}
