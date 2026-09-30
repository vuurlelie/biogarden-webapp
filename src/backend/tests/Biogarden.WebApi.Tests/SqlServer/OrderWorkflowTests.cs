using System.Net;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.Constants;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class OrderWorkflowTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task GuestCart_TwoDifferentProductsRemainInTheSameCartAcrossReads()
    {
        var firstProduct = await SeedProductAsync();
        var secondProduct = await SeedProductAsync();
        using var client = _app.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var initial = (await client.GetFromJsonAsync<CartDto>("/api/cart"))!;
        await AddToCartAsync(client, firstProduct);
        await AddToCartAsync(client, secondProduct);
        for (var read = 0; read < 2; read++)
        {
            var cart = (await client.GetFromJsonAsync<CartDto>("/api/cart"))!;
            Assert.Equal(initial.Id, cart.Id);
            Assert.Equal(2, cart.TotalItemCount);
            Assert.Equal(new[] { firstProduct, secondProduct }, cart.Items.Select(item => item.ProductId).Order().ToArray());
        }
        await using var database = _app.OpenDatabase();
        Assert.Equal(initial.Id, (await database.Carts.SingleAsync()).Id);
        using var otherVisitor = _app.Factory.CreateClient();
        var otherCart = (await otherVisitor.GetFromJsonAsync<CartDto>("/api/cart"))!;
        Assert.NotEqual(initial.Id, otherCart.Id);
        Assert.Empty(otherCart.Items);
    }

    [SqlServerFact]
    public async Task GuestCheckout_PersistsOneOrderAndTwoEmails_WhenEmailMatchesAccount()
    {
        using (var scope = _app.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var result = await users.CreateAsync(new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "customer@example.com",
                Email = "customer@example.com",
                EmailConfirmed = true
            });
            Assert.True(result.Succeeded);
        }
        _app.Sender.Fail = true;
        var productId = await SeedProductAsync();
        using var client = _app.Factory.CreateClient();
        await AddToCartAsync(client, productId);
        var request = await Request().WithQuoteAsync(client);
        using var first = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var order = (await first.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.EndsWith($"/api/Orders/{order.Id}", first.Headers.Location?.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        using var retry = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(order.Id, (await retry.Content.ReadFromJsonAsync<OrderDto>())!.Id);

        await using var database = _app.OpenDatabase();
        var saved = await database.Orders.Include(x => x.Items).SingleAsync();
        Assert.Null(saved.UserId);
        Assert.Equal(900m, saved.TotalPrice);
        Assert.Single(saved.Items);
        Assert.Equal(2, (await database.Products.SingleAsync()).StockQuantity);
        Assert.True((await database.Carts.SingleAsync()).IsCheckedOut);
        Assert.Equal(2, await database.OrderEmails.CountAsync());
        Assert.Empty(_app.Sender.Sent);

        using var stranger = _app.Factory.CreateClient();
        using var unauthorizedRetry = await stranger.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.NotFound, unauthorizedRetry.StatusCode);
    }

    [SqlServerFact]
    public async Task CartMutation_DistinguishesInvalidInputMissingProductAndStockConflict()
    {
        var productId = await SeedProductAsync();
        using var client = _app.Factory.CreateClient();
        using var invalid = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = productId, Quantity = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var missing = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = int.MaxValue, Quantity = 1 });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var conflict = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = productId, Quantity = 4 });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [SqlServerFact]
    public async Task QueuePersistenceFailure_RollsBackOrderStockAndCart()
    {
        var productId = await SeedProductAsync();
        using var client = _app.Factory.CreateClient();
        await AddToCartAsync(client, productId);
        _app.QueueFailure.Enabled = true;
        using var failed = await client.PostAsJsonAsync("/api/orders", await Request().WithQuoteAsync(client));
        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);

        await using var database = _app.OpenDatabase();
        Assert.Empty(await database.Orders.ToListAsync());
        Assert.Empty(await database.OrderEmails.ToListAsync());
        Assert.Equal(3, (await database.Products.SingleAsync()).StockQuantity);
        Assert.False((await database.Carts.SingleAsync()).IsCheckedOut);
    }

    [SqlServerFact]
    public async Task EmailRetry_SurvivesApiRestartAndClearsDeliveredPayloads()
    {
        await PlaceOrderAsync();
        _app.Sender.Fail = true;
        using (var scope = _app.Factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<OrderEmailDispatcher>();
            Assert.True(await dispatcher.DispatchNextAsync(default));
            Assert.True(await dispatcher.DispatchNextAsync(default));
            Assert.False(await dispatcher.DispatchNextAsync(default));
        }
        await using (var database = _app.OpenDatabase())
        {
            var queued = await database.OrderEmails.ToListAsync();
            Assert.All(queued, email =>
            {
                Assert.Null(email.SentAtUtc);
                Assert.Equal(1, email.Attempts);
                Assert.Null(email.LeaseId);
                Assert.False(string.IsNullOrEmpty(email.HtmlBody));
            });
            await database.OrderEmails.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        }
        await _app.RestartApiAsync();
        _app.Sender.Fail = false;
        using (var scope = _app.Factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<OrderEmailDispatcher>();
            Assert.True(await dispatcher.DispatchNextAsync(default));
            Assert.True(await dispatcher.DispatchNextAsync(default));
            Assert.False(await dispatcher.DispatchNextAsync(default));
        }
        Assert.Equal(2, _app.Sender.Sent.Count);
        Assert.Equal(2, _app.Sender.Sent.Select(x => x.To).Distinct().Count());
        await using var check = _app.OpenDatabase();
        Assert.All(await check.OrderEmails.ToListAsync(), email =>
        {
            Assert.NotNull(email.SentAtUtc);
            Assert.Equal(2, email.Attempts);
            Assert.Empty(email.Recipient);
            Assert.Null(email.HtmlBody);
            Assert.Null(email.PlainTextBody);
        });
    }

    [SqlServerFact]
    public async Task ConcurrentClaims_GrantOnlyOneLease_AndExpiredLeaseCannotCompleteNewClaim()
    {
        await PlaceOrderAsync();
        await using (var database = _app.OpenDatabase())
        {
            await database.OrderEmails.Where(x => x.Kind == "business").ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.NextAttemptAtUtc, DateTime.UtcNow.AddHours(1)));
        }
        using var firstScope = _app.Factory.Services.CreateScope();
        using var secondScope = _app.Factory.Services.CreateScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IOrderEmailRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IOrderEmailRepository>();
        var now = DateTime.UtcNow;
        var claims = await Task.WhenAll(firstRepository.TryClaimAsync(now, default), secondRepository.TryClaimAsync(now, default));
        var firstClaim = Assert.Single(claims, x => x is not null)!;
        var newClaim = await secondRepository.TryClaimAsync(now.AddMinutes(3), default);
        Assert.NotNull(newClaim);
        Assert.Equal(firstClaim.Id, newClaim.Id);
        Assert.NotEqual(firstClaim.LeaseId, newClaim.LeaseId);

        await firstRepository.CompleteAsync(firstClaim.Id, firstClaim.LeaseId!.Value, now, default);
        await using var check = _app.OpenDatabase();
        var pending = await check.OrderEmails.AsNoTracking().SingleAsync(x => x.Id == newClaim.Id);
        Assert.Null(pending.SentAtUtc);
        Assert.Equal(newClaim.LeaseId, pending.LeaseId);
        await secondRepository.CompleteAsync(newClaim.Id, newClaim.LeaseId!.Value, now.AddMinutes(3), default);
        Assert.NotNull((await check.OrderEmails.AsNoTracking().SingleAsync(x => x.Id == newClaim.Id)).SentAtUtc);
    }

    [SqlServerFact]
    public async Task ConcurrentInventoryUpdates_RejectStaleRowVersion()
    {
        var id = await SeedProductAsync();
        await using var first = _app.OpenDatabase();
        await using var second = _app.OpenDatabase();
        var a = await first.Products.SingleAsync(x => x.Id == id);
        var b = await second.Products.SingleAsync(x => x.Id == id);
        a.StockQuantity = 2;
        b.StockQuantity = 1;
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var check = _app.OpenDatabase();
        Assert.Equal(2, (await check.Products.SingleAsync(x => x.Id == id)).StockQuantity);
    }

    [SqlServerFact]
    public async Task StalePaymentConfirmation_CannotOverwriteCancellation_AndFreshRetryReconcilesPayment()
    {
        await PlaceOrderAsync();
        Guid orderId;
        await using (var database = _app.OpenDatabase())
        {
            var order = await database.Orders.SingleAsync();
            orderId = order.Id;
            order.SumUpCheckoutId = "concurrent-checkout";
            await database.SaveChangesAsync();
        }

        using var paymentScope = _app.Factory.Services.CreateScope();
        var paymentDatabase = paymentScope.ServiceProvider.GetRequiredService<Biogarden.DataAccess.AppDbContext>();
        // Hold the snapshot a payment request read before cancellation commits.
        await paymentDatabase.Orders.SingleAsync(x => x.Id == orderId);
        using (var cancelScope = _app.Factory.Services.CreateScope())
        {
            var admin = cancelScope.ServiceProvider.GetRequiredService<Biogarden.BusinessLogic.Abstractions.IAdminOrderService>();
            await admin.UpdateAsync(orderId, new Biogarden.BusinessLogic.DTOs.Admin.AdminOrderUpdateDto
            {
                Status = OrderStatus.Cancelled,
                PaymentStatus = PaymentStatus.Pending
            });
        }
        var stalePayment = paymentScope.ServiceProvider.GetRequiredService<Biogarden.BusinessLogic.Abstractions.IOrderService>();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            stalePayment.ConfirmSumUpCheckoutAsync("concurrent-checkout", "transaction", 900m, "HUF"));

        using (var retryScope = _app.Factory.Services.CreateScope())
        {
            var retry = retryScope.ServiceProvider.GetRequiredService<Biogarden.BusinessLogic.Abstractions.IOrderService>();
            await retry.ConfirmSumUpCheckoutAsync("concurrent-checkout", "transaction", 900m, "HUF");
        }
        await using var check = _app.OpenDatabase();
        var saved = await check.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Cancelled, saved.Status);
        Assert.Equal(PaymentStatus.RefundPending, saved.PaymentStatus);
        Assert.False(saved.StockCommitted);
        Assert.Equal(3, (await check.Products.SingleAsync()).StockQuantity);
    }

    private async Task<int> SeedProductAsync()
    {
        await using var database = _app.OpenDatabase();
        var product = new Product { Name = "Integration test product", Available = true, Price = 900m, StockQuantity = 3 };
        database.Products.Add(product);
        await database.SaveChangesAsync();
        return product.Id;
    }

    [SqlServerFact]
    public async Task AdminStatusChanges_QueueDistinctEvents_AndRollbackWhenQueueSaveFails()
    {
        await PlaceOrderAsync();
        await using var check = _app.OpenDatabase();
        var orderId = (await check.Orders.AsNoTracking().SingleAsync()).Id;
        _app.Sender.Fail = true;
        _app.QueueFailure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => ChangeStatusAsync(orderId, OrderStatus.Confirmed));
        Assert.Equal(OrderStatus.Pending, (await check.Orders.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(2, await check.OrderEmails.CountAsync());

        _app.QueueFailure.Enabled = false;
        await ChangeStatusAsync(orderId, OrderStatus.Confirmed);
        await ChangeStatusAsync(orderId, OrderStatus.Confirmed);
        await ChangeStatusAsync(orderId, OrderStatus.Shipped);
        var notifications = await check.OrderEmails.Where(x => x.Kind == "status").ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Equal(2, notifications.Select(x => x.EventId).Distinct().Count());
        Assert.Empty(_app.Sender.Sent);
        await _app.RestartApiAsync();
        _app.Sender.Fail = false;
        using var scope = _app.Factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OrderEmailDispatcher>();
        for (var i = 0; i < 4; i++) Assert.True(await dispatcher.DispatchNextAsync(default));
        Assert.False(await dispatcher.DispatchNextAsync(default));
        Assert.Equal(4, _app.Sender.Sent.Count);
    }

    [SqlServerFact]
    public async Task WithdrawalRequest_QueuesBothRecipients_AndRollbackLeavesNoRequest()
    {
        await PlaceOrderAsync();
        await using var check = _app.OpenDatabase();
        var order = await check.Orders.AsNoTracking().SingleAsync();
        async Task RequestWithdrawalAsync()
        {
            using var scope = _app.Factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<Biogarden.BusinessLogic.Abstractions.IOrderService>();
            await service.RequestWithdrawalByOrderLookupAsync(new PublicOrderWithdrawalRequestDto
            {
                OrderNumber = order.OrderNumber,
                CustomerEmail = order.CustomerEmail,
                Reason = "Please cancel this test order."
            });
        }
        _app.Sender.Fail = true;
        _app.QueueFailure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(RequestWithdrawalAsync);
        Assert.Empty(await check.OrderWithdrawalRequests.ToListAsync());
        Assert.Equal(2, await check.OrderEmails.CountAsync());
        _app.QueueFailure.Enabled = false;
        using var client = _app.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/orders/withdrawal-requests", new PublicOrderWithdrawalRequestDto
        {
            OrderNumber = order.OrderNumber,
            CustomerEmail = order.CustomerEmail,
            Reason = "Please cancel this test order."
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<OrderWithdrawalRequestDto>());
        Assert.Single(await check.OrderWithdrawalRequests.ToListAsync());
        var notifications = await check.OrderEmails.Where(x => x.Kind.StartsWith("withdrawal-")).ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Single(notifications.Select(x => x.EventId).Distinct());
        Assert.All(notifications, email => Assert.NotEqual(Guid.Empty, email.EventId));
        Assert.Empty(_app.Sender.Sent);
    }

    private async Task ChangeStatusAsync(Guid orderId, OrderStatus status)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<Biogarden.BusinessLogic.Abstractions.IAdminOrderService>();
        await service.UpdateAsync(orderId, new Biogarden.BusinessLogic.DTOs.Admin.AdminOrderUpdateDto
        {
            Status = status,
            PaymentStatus = PaymentStatus.Pending
        });
    }

    private static async Task AddToCartAsync(HttpClient client, int productId)
    {
        using var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = productId, Quantity = 1 });
        response.EnsureSuccessStatusCode();
    }

    private async Task PlaceOrderAsync()
    {
        var productId = await SeedProductAsync();
        using var client = _app.Factory.CreateClient();
        await AddToCartAsync(client, productId);
        using var response = await client.PostAsJsonAsync("/api/orders", await Request().WithQuoteAsync(client));
        response.EnsureSuccessStatusCode();
    }

    private static PlaceOrderDto Request() => new()
    {
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        CustomerName = "Test Customer",
        CustomerEmail = "customer@example.com",
        CustomerPhone = "+361234567",
        FulfillmentMethod = FulfillmentMethod.PersonalPickup,
        AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
        AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion
    };
}
