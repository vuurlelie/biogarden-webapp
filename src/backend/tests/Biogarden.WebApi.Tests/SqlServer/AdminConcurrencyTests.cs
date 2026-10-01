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
        var fresh = (await service.GetAllAsync()).Single();
        Assert.Equal(PaymentStatus.Paid, fresh.PaymentStatus);
        var saved = await service.UpdateAsync(order.Id, new AdminOrderUpdateDto { Version = fresh.Version, Status = OrderStatus.Shipped, PaymentStatus = PaymentStatus.Paid });
        Assert.NotEqual(fresh.Version, saved.Version);
        Assert.Equal(PaymentStatus.Paid, saved.PaymentStatus);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(order.Id,
            new AdminOrderUpdateDto { Version = fresh.Version, Status = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid }));
        Assert.Equal(OrderStatus.Shipped, (await db.Orders.AsNoTracking().SingleAsync()).Status);
    }
}
