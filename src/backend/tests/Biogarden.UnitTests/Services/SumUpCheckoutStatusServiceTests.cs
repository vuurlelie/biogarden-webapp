using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Models.Payments;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class SumUpCheckoutStatusServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);
    private readonly Mock<IPaymentProviderEventRepository> _paymentProviderEvents = new(MockBehavior.Strict);
    private readonly Mock<ISumUpCheckoutService> _checkoutService = new(MockBehavior.Strict);
    private readonly Mock<IOrderService> _orderService = new(MockBehavior.Strict);

    [Fact]
    public async Task HandleAsync_ForPaidCheckout_ConfirmsOrderPayment()
    {
        var payload = CreateCheckoutStatusPayload("checkout_123");
        var service = CreateService();
        SetupEventInsert("PAID");

        _checkoutService
            .Setup(service => service.GetCheckoutAsync("checkout_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult("checkout_123", "PAID", TransactionCode: "txn_123", Amount: 3600, Currency: "HUF"));
        _orderService
            .Setup(service => service.ReconcileSumUpCheckoutAsync(
                It.Is<SumUpCheckoutResult>(checkoutResult => checkoutResult.CheckoutId == "checkout_123" && checkoutResult.Status == "PAID" && checkoutResult.Amount == 3600 && checkoutResult.Currency == "HUF"),
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await service.HandleAsync(payload);

        _checkoutService.VerifyAll();
        _orderService.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_ForFailedCheckout_MarksOrderPaymentFailed()
    {
        var payload = CreateCheckoutStatusPayload("checkout_123");
        var service = CreateService();
        SetupEventInsert("FAILED");

        _checkoutService
            .Setup(service => service.GetCheckoutAsync("checkout_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult("checkout_123", "FAILED", TransactionCode: "txn_123"));
        _orderService
            .Setup(service => service.ReconcileSumUpCheckoutAsync(
                It.Is<SumUpCheckoutResult>(checkoutResult => checkoutResult.CheckoutId == "checkout_123" && (checkoutResult.Status == "FAILED" || checkoutResult.Status == "EXPIRED")),
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await service.HandleAsync(payload);

        _checkoutService.VerifyAll();
        _orderService.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_ForExpiredCheckout_MarksOrderPaymentFailed()
    {
        var payload = CreateCheckoutStatusPayload("checkout_123");
        var service = CreateService();
        SetupEventInsert("EXPIRED");

        _checkoutService
            .Setup(service => service.GetCheckoutAsync("checkout_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult("checkout_123", "EXPIRED", TransactionCode: "txn_123"));
        _orderService
            .Setup(service => service.ReconcileSumUpCheckoutAsync(
                It.Is<SumUpCheckoutResult>(checkoutResult => checkoutResult.CheckoutId == "checkout_123" && (checkoutResult.Status == "FAILED" || checkoutResult.Status == "EXPIRED")),
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await service.HandleAsync(payload);

        _checkoutService.VerifyAll();
        _orderService.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_ForUnknownEventType_IgnoresPayload()
    {
        var service = CreateService();

        await service.HandleAsync("""
        {
          "event_type": "UNKNOWN_EVENT",
          "id": "checkout_123"
        }
        """);

        _checkoutService.Verify(service => service.GetCheckoutAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(
            service => service.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task<PaymentProviderEvent>>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleAsync_WhenCheckoutLookupFails_DoesNotRecordProcessedEvent()
    {
        var payload = CreateCheckoutStatusPayload("checkout_123");
        var service = CreateService();

        _checkoutService
            .Setup(service => service.GetCheckoutAsync("checkout_123", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Checkout not found."));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.HandleAsync(payload));

        _unitOfWork.Verify(
            service => service.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task<PaymentProviderEvent>>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _orderService.Verify(
            service => service.ConfirmSumUpCheckoutAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleAsync_WhenEventWasAlreadyReceived_IgnoresDuplicate()
    {
        var payload = CreateCheckoutStatusPayload("checkout_123");
        var service = CreateService();
        _checkoutService
            .Setup(service => service.GetCheckoutAsync("checkout_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult("checkout_123", "PAID", TransactionCode: "txn_123", Amount: 3600, Currency: "HUF"));
        _unitOfWork.SetupGet(service => service.PaymentProviderEventRepository)
            .Returns(_paymentProviderEvents.Object);
        _paymentProviderEvents
            .Setup(repository => repository.GetByProviderAndEventKeyAsync("SumUp", "CHECKOUT_STATUS_CHANGED:checkout_123:PAID", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentProviderEvent
            {
                Id = Guid.NewGuid(),
                Provider = "SumUp",
                EventKey = "CHECKOUT_STATUS_CHANGED:checkout_123:PAID",
                CheckoutId = "checkout_123",
                EventType = "CHECKOUT_STATUS_CHANGED",
                Status = "PAID",
                ProcessedAtUtc = DateTime.UtcNow
            });

        await service.HandleAsync(payload);

        _checkoutService.Verify(service => service.GetCheckoutAsync("checkout_123", It.IsAny<CancellationToken>()), Times.Once);
        _orderService.Verify(
            service => service.ConfirmSumUpCheckoutAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleAsync_WhenFetchedCheckoutIdDoesNotMatchEvent_ThrowsBeforeUpdatingOrder()
    {
        var payload = CreateCheckoutStatusPayload("checkout_123");
        var service = CreateService();

        _checkoutService
            .Setup(service => service.GetCheckoutAsync("checkout_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult("checkout_456", "PAID", TransactionCode: "txn_123", Amount: 3600, Currency: "HUF"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HandleAsync(payload));

        _orderService.Verify(
            service => service.ConfirmSumUpCheckoutAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleAsync_WhenCheckoutIsPending_RecordsPendingEventWithoutUpdatingOrder()
    {
        var payload = CreateCheckoutStatusPayload("checkout_123");
        var service = CreateService();
        SetupEventInsert("PENDING");
        _orderService.Setup(orderService => orderService.ReconcileSumUpCheckoutAsync(It.Is<SumUpCheckoutResult>(checkoutResult => checkoutResult.Status == "PENDING"), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _checkoutService
            .Setup(service => service.GetCheckoutAsync("checkout_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult("checkout_123", "PENDING"));
        _unitOfWork.Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await service.HandleAsync(payload);

        _orderService.Verify(
            service => service.ConfirmSumUpCheckoutAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _orderService.Verify(service => service.MarkSumUpPaymentFailedAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenOrderUpdateFails_StoresProcessingErrorAndRethrows()
    {
        var payload = CreateCheckoutStatusPayload("checkout_123");
        var service = CreateService();
        PaymentProviderEvent? capturedEvent = null;
        SetupEventInsert("PAID", paymentEvent => capturedEvent = paymentEvent);

        _checkoutService
            .Setup(service => service.GetCheckoutAsync("checkout_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult("checkout_123", "PAID", TransactionCode: "txn_123", Amount: 3600, Currency: "HUF"));
        _orderService
            .Setup(service => service.ReconcileSumUpCheckoutAsync(
                It.Is<SumUpCheckoutResult>(checkoutResult => checkoutResult.CheckoutId == "checkout_123" && checkoutResult.Status == "PAID" && checkoutResult.Amount == 3600 && checkoutResult.Currency == "HUF"),
                It.IsAny<CancellationToken>()
            ))
            .ThrowsAsync(new InvalidOperationException("SumUp checkout amount does not match the order total."));
        _unitOfWork.Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.HandleAsync(payload));

        Assert.Equal("SumUp checkout amount does not match the order total.", exception.Message);
        Assert.NotNull(capturedEvent);
        Assert.NotNull(capturedEvent.LastProcessingAttemptAtUtc);
        Assert.NotNull(capturedEvent.ProcessingFailedAtUtc);
        Assert.Equal("SumUp checkout amount does not match the order total.", capturedEvent.ProcessingError);
        Assert.Null(capturedEvent.ProcessedAtUtc);
    }

    private SumUpCheckoutStatusService CreateService()
    {
        _unitOfWork.Setup(unitOfWork => unitOfWork.ClearTrackedChanges());
        return new SumUpCheckoutStatusService(_unitOfWork.Object, _checkoutService.Object, _orderService.Object);
    }

    private void SetupEventInsert(string status, Action<PaymentProviderEvent>? onAdd = null)
    {
        _unitOfWork.SetupGet(service => service.PaymentProviderEventRepository)
            .Returns(_paymentProviderEvents.Object);
        PaymentProviderEvent? stored = null;
        _paymentProviderEvents
            .Setup(repository => repository.GetByProviderAndEventKeyAsync("SumUp", $"CHECKOUT_STATUS_CHANGED:checkout_123:{status}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stored);
        _unitOfWork
            .Setup(service => service.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task<PaymentProviderEvent>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<PaymentProviderEvent>>, CancellationToken>(
                (operation, token) => operation(token));
        _paymentProviderEvents
            .Setup(repository => repository.AddAsync(
                It.Is<PaymentProviderEvent>(paymentEvent =>
                    paymentEvent.Provider == "SumUp" &&
                    paymentEvent.EventKey == $"CHECKOUT_STATUS_CHANGED:checkout_123:{status}" &&
                    paymentEvent.CheckoutId == "checkout_123" &&
                    paymentEvent.Status == status),
                It.IsAny<CancellationToken>()
            ))
            .Callback<PaymentProviderEvent, CancellationToken>((paymentEvent, _) => { stored = paymentEvent; onAdd?.Invoke(paymentEvent); })
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private static string CreateCheckoutStatusPayload(string checkoutId)
    {
        return $$"""
        {
          "event_type": "CHECKOUT_STATUS_CHANGED",
          "id": "{{checkoutId}}"
        }
        """;
    }

    private static OrderDto CreateOrderDto()
    {
        return new OrderDto
        {
            Id = Guid.NewGuid(),
            OrderNumber = "BK-TEST-0001"
        };
    }
}
