using System.Net.Http.Json;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Constants;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.DTOs.Products;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class AdminConcurrencyTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    private async Task<(int ProductId, OrderDto Order, string ProductVersion)> PurchaseAsync()
    {
        using var client = _app.Factory.CreateClient();
        await using var db = _app.OpenDatabase();
        var product = new Product { Name = "Concurrency tea", Price = 900, Available = true, StockQuantity = 3 };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        (await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = product.Id, Quantity = 1 })).EnsureSuccessStatusCode();
        var beforeCheckout = Convert.ToBase64String((await db.Products.AsNoTracking().SingleAsync()).RowVersion);
        var request = new PlaceOrderDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            FulfillmentMethod = FulfillmentMethod.PersonalPickup,
            PaymentMethod = OrderPaymentMethod.OnlineCard,
            CustomerName = "Test Customer",
            CustomerEmail = "test@example.com",
            CustomerPhone = "+36301234567",
            AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
            AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion
        };
        using var response = await client.PostAsJsonAsync("/api/orders", await request.WithQuoteAsync(client));
        response.EnsureSuccessStatusCode();
        return (product.Id, (await response.Content.ReadFromJsonAsync<OrderDto>())!, beforeCheckout);
    }

    [SqlServerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FormOpenedBeforeCheckoutCannotRestoreSoldStock(bool inventory)
    {
        var (id, _, version) = await PurchaseAsync();
        using var scope = _app.Factory.Services.CreateScope();
        if (inventory)
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IAdminInventoryService>()
                .UpdateAsync(id, new UpdateAdminInventoryDto { Version = version, StockQuantity = 3, Available = true }));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IProductService>()
                .UpdateAsync(id, new ProductEditDto { Version = version, Name = "Old form", Price = 900, StockQuantity = 3, Available = true }));
        await using var db = _app.OpenDatabase();
        Assert.Equal(2, (await db.Products.SingleAsync()).StockQuantity);
    }

    [SqlServerFact]
    public async Task FormOpenedBeforeVerifiedPaymentCannotErasePaymentAndFreshVersionCanAdvanceStatus()
    {
        var (_, order, _) = await PurchaseAsync();
        await using var db = _app.OpenDatabase();
        await db.Orders.Where(x => x.Id == order.Id).ExecuteUpdateAsync(x => x.SetProperty(o => o.SumUpCheckoutId, "verified-checkout"));
        using (var paymentScope = _app.Factory.Services.CreateScope())
            await paymentScope.ServiceProvider.GetRequiredService<IOrderService>().ConfirmSumUpCheckoutAsync("verified-checkout", "transaction", order.TotalPrice, "HUF");
        using var scope = _app.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminOrderService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(order.Id,
            new AdminOrderUpdateDto { Version = order.Version, Status = OrderStatus.Confirmed, PaymentStatus = PaymentStatus.Pending }));
        var fresh = (await service.GetPageAsync(new())).Items.Single();
        Assert.Equal(PaymentStatus.Paid, fresh.PaymentStatus);
        var saved = await service.UpdateAsync(order.Id, new AdminOrderUpdateDto { Version = fresh.Version, Status = OrderStatus.Shipped, PaymentStatus = PaymentStatus.Paid });
        Assert.NotEqual(fresh.Version, saved.Version);
        Assert.Equal(PaymentStatus.Paid, saved.PaymentStatus);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(order.Id,
            new AdminOrderUpdateDto { Version = fresh.Version, Status = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid }));
        Assert.Equal(OrderStatus.Shipped, (await db.Orders.AsNoTracking().SingleAsync()).Status);
    }

    [SqlServerFact]
    public async Task PaymentCorrectionRequiresActorReasonAndCommitsAuditAtomically()
    {
        var (_, order, _) = await PurchaseAsync();
        await using var db = _app.OpenDatabase();
        await db.Orders.Where(x => x.Id == order.Id).ExecuteUpdateAsync(x => x.SetProperty(o => o.PaymentMethod, OrderPaymentMethod.PayAtPickup));
        var actor = Guid.NewGuid();
        async Task<OrderDto> CorrectAsync(Guid? actorId, string? reason, bool advanceStatus)
        {
            using var scope = _app.Factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAdminOrderService>();
            var fresh = (await service.GetPageAsync(new())).Items.Single();
            return await service.UpdateAsync(order.Id, new AdminOrderUpdateDto
            {
                Version = fresh.Version,
                Status = advanceStatus ? OrderStatus.Confirmed : fresh.Status,
                PaymentStatus = PaymentStatus.Paid,
                PaymentCorrectionReason = reason
            }, actorId: actorId);
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CorrectAsync(null, "Cash received", false));
        await Assert.ThrowsAsync<ArgumentException>(() => CorrectAsync(actor, " ", false));
        _app.QueueFailure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CorrectAsync(actor, "Cash received", true));
        Assert.Empty(await db.OrderPaymentCorrections.ToListAsync());
        Assert.Equal(PaymentStatus.Pending, (await db.Orders.AsNoTracking().SingleAsync()).PaymentStatus);
        _app.QueueFailure.Enabled = false;
        await CorrectAsync(actor, "Cash received", true);
        await CorrectAsync(actor, "Retry", true);
        var audit = Assert.Single(await db.OrderPaymentCorrections.ToListAsync());
        Assert.Equal(actor, audit.ActorId);
        Assert.Equal("Cash received", audit.Reason);
        Assert.Equal(PaymentStatus.Pending, audit.PreviousStatus);
        Assert.Equal(PaymentStatus.Paid, audit.NewStatus);
        Assert.Equal(PaymentStatus.Paid, (await db.Orders.AsNoTracking().SingleAsync()).PaymentStatus);
    }

    [SqlServerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentAdminCancellationRestoresStockAndQueuesNotificationOnlyOnce(bool paid)
    {
        var (_, order, _) = await PurchaseAsync();
        if (paid)
        {
            await using var setup = _app.OpenDatabase();
            await setup.Orders.Where(x => x.Id == order.Id).ExecuteUpdateAsync(x => x.SetProperty(o => o.PaymentStatus, PaymentStatus.Paid));
            using var scope = _app.Factory.Services.CreateScope();
            order = (await scope.ServiceProvider.GetRequiredService<IAdminOrderService>().GetPageAsync(new())).Items.Single();
        }
        _app.SaveGate.EntityType = typeof(Biogarden.DataAccess.Entities.Order);
        _app.SaveGate.State = EntityState.Modified;
        async Task<Exception?> CancelAsync()
        {
            using var scope = _app.Factory.Services.CreateScope();
            return await Record.ExceptionAsync(() => scope.ServiceProvider.GetRequiredService<IAdminOrderService>().UpdateAsync(order.Id,
                new AdminOrderUpdateDto
                {
                    Version = order.Version,
                    Status = OrderStatus.Cancelled,
                    PaymentStatus = order.PaymentStatus,
                    PaymentCorrectionReason = "Customer requested cancellation; refund pending"
                }, actorId: Guid.NewGuid()));
        }
        var first = CancelAsync();
        var second = CancelAsync();
        try { await _app.SaveGate.WaitForBothAsync(); }
        finally { _app.SaveGate.Release(); }
        var results = await Task.WhenAll(first, second);
        Assert.Single(results, result => result is null);
        Assert.Single(results, result => result is DbUpdateConcurrencyException);
        await using var db = _app.OpenDatabase();
        Assert.Equal(3, (await db.Products.SingleAsync()).StockQuantity);
        Assert.False((await db.Orders.SingleAsync()).StockCommitted);
        Assert.Equal(1, await db.OrderEmails.CountAsync(x => x.Kind == "status"));
        Assert.Equal(paid ? 1 : 0, await db.OrderPaymentCorrections.CountAsync());
        Assert.Equal(paid ? PaymentStatus.RefundPending : PaymentStatus.Pending, (await db.Orders.SingleAsync()).PaymentStatus);
    }

    [SqlServerFact]
    public async Task LegacyUncommittedOrderCannotConsumeAnotherCartsReservation()
    {
        var (id, order, _) = await PurchaseAsync();
        await using var db = _app.OpenDatabase();
        await db.Orders.Where(x => x.Id == order.Id).ExecuteUpdateAsync(x => x.SetProperty(o => o.StockCommitted, false));
        using var customer = _app.Factory.CreateClient();
        (await customer.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = id, Quantity = 2 })).EnsureSuccessStatusCode();
        using var scope = _app.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminOrderService>();
        var fresh = (await service.GetPageAsync(new())).Items.Single();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(order.Id,
            new AdminOrderUpdateDto { Version = fresh.Version, Status = OrderStatus.Confirmed, PaymentStatus = PaymentStatus.Pending }));
        Assert.Equal(2, (await db.Products.SingleAsync()).StockQuantity);
        Assert.Equal(OrderStatus.Pending, (await db.Orders.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(2, await db.OrderEmails.CountAsync());
    }

    [SqlServerFact]
    public async Task CancelledPartialRefundCanBeCompletedWithoutReopeningRefundOrRestoringStockAgain()
    {
        var (_, order, _) = await PurchaseAsync();
        await using var db = _app.OpenDatabase();
        await db.Orders.Where(x => x.Id == order.Id).ExecuteUpdateAsync(x => x
            .SetProperty(o => o.Status, OrderStatus.Cancelled).SetProperty(o => o.PaymentStatus, PaymentStatus.PartiallyRefunded)
            .SetProperty(o => o.StockCommitted, false));
        using var scope = _app.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminOrderService>();
        var fresh = (await service.GetPageAsync(new())).Items.Single();
        var saved = await service.UpdateAsync(order.Id, new AdminOrderUpdateDto
        {
            Version = fresh.Version,
            Status = OrderStatus.Cancelled,
            PaymentStatus = PaymentStatus.Refunded,
            PaymentCorrectionReason = "Remaining refund completed"
        }, actorId: Guid.NewGuid());
        Assert.Equal(PaymentStatus.Refunded, saved.PaymentStatus);
        Assert.Equal(2, (await db.Products.SingleAsync()).StockQuantity);
        Assert.Single(await db.OrderPaymentCorrections.ToListAsync());
        Assert.Equal(2, await db.OrderEmails.CountAsync());
    }
}
