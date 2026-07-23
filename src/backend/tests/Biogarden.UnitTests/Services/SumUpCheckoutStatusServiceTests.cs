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
            .Setup(service => service.GetCheckoutAsync(
                "checkout_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult(
                "checkout_123",
                "PAID",
                TransactionCode: "txn_123",
                Amount: 3600,
                Currency: "HUF"));
        _orderService
            .Setup(service => service.ConfirmSumUpCheckoutAsync(
                "checkout_123",
                "txn_123",
                3600,
                "HUF",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderDto());
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
            .Setup(service => service.GetCheckoutAsync(
                "checkout_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult(
                "checkout_123",
                "FAILED",
                TransactionCode: "txn_123"));
        _orderService
            .Setup(service => service.MarkSumUpPaymentFailedAsync(
                "checkout_123",
                "txn_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderDto());
        _unitOfWork.Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await service.HandleAsync(payload);

        _checkoutService.VerifyAll();
        _orderService.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_WhenEventWasAlreadyReceived_IgnoresDuplicate()
    {
        var payload = CreateCheckoutStatusPayload("checkout_123");
        var service = CreateService();
        _checkoutService
            .Setup(service => service.GetCheckoutAsync(
                "checkout_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult(
                "checkout_123",
                "PAID",
                TransactionCode: "txn_123",
                Amount: 3600,
                Currency: "HUF"));
        _unitOfWork.SetupGet(service => service.PaymentProviderEventRepository)
            .Returns(_paymentProviderEvents.Object);
        _paymentProviderEvents
            .Setup(repository => repository.GetByProviderAndEventKeyAsync(
                "SumUp",
                "CHECKOUT_STATUS_CHANGED:checkout_123:PAID",
                It.IsAny<CancellationToken>()))
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

        _checkoutService.Verify(
            service => service.GetCheckoutAsync("checkout_123", It.IsAny<CancellationToken>()),
            Times.Once);
        _orderService.Verify(
            service => service.ConfirmSumUpCheckoutAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenFetchedCheckoutIdDoesNotMatchEvent_ThrowsBeforeUpdatingOrder()
    {
        var payload = CreateCheckoutStatusPayload("checkout_123");
        var service = CreateService();

        _checkoutService
            .Setup(service => service.GetCheckoutAsync(
                "checkout_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult(
                "checkout_456",
                "PAID",
                TransactionCode: "txn_123",
                Amount: 3600,
                Currency: "HUF"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HandleAsync(payload));

        _orderService.Verify(
            service => service.ConfirmSumUpCheckoutAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCheckoutIsPending_RecordsPendingEventWithoutUpdatingOrder()
    {
        var payload = CreateCheckoutStatusPayload("checkout_123");
        var service = CreateService();
        SetupEventInsert("PENDING");

        _checkoutService
            .Setup(service => service.GetCheckoutAsync(
                "checkout_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult(
                "checkout_123",
                "PENDING"));
        _unitOfWork.Setup(service => service.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await service.HandleAsync(payload);

        _orderService.Verify(
            service => service.ConfirmSumUpCheckoutAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _orderService.Verify(
            service => service.MarkSumUpPaymentFailedAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenOrderUpdateFails_StoresProcessingErrorAndRethrows()
    {
        var payload = CreateCheckoutStatusPayload("checkout_123");
        var service = CreateService();
        PaymentProviderEvent? capturedEvent = null;
        SetupEventInsert("PAID", paymentEvent => capturedEvent = paymentEvent);

        _checkoutService
            .Setup(service => service.GetCheckoutAsync(
                "checkout_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult(
                "checkout_123",
                "PAID",
                TransactionCode: "txn_123",
                Amount: 3600,
                Currency: "HUF"));
        _orderService
            .Setup(service => service.ConfirmSumUpCheckoutAsync(
                "checkout_123",
                "txn_123",
                3600,
                "HUF",
                It.IsAny<CancellationToken>()))
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
        return new SumUpCheckoutStatusService(
            _unitOfWork.Object,
            _checkoutService.Object,
            _orderService.Object);
    }

    private void SetupEventInsert(string status, Action<PaymentProviderEvent>? onAdd = null)
    {
        _unitOfWork.SetupGet(service => service.PaymentProviderEventRepository)
            .Returns(_paymentProviderEvents.Object);
        _paymentProviderEvents
            .Setup(repository => repository.GetByProviderAndEventKeyAsync(
                "SumUp",
                $"CHECKOUT_STATUS_CHANGED:checkout_123:{status}",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentProviderEvent?)null);
        _unitOfWork
            .Setup(service => service.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<PaymentProviderEvent>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<PaymentProviderEvent>>, CancellationToken>(
                (operation, token) => operation(token));
        _paymentProviderEvents
            .Setup(repository => repository.AddAsync(
                It.Is<PaymentProviderEvent>(paymentEvent =>
                    paymentEvent.Provider == "SumUp" &&
                    paymentEvent.EventKey == $"CHECKOUT_STATUS_CHANGED:checkout_123:{status}" &&
                    paymentEvent.CheckoutId == "checkout_123" &&
                    paymentEvent.Status == status),
                It.IsAny<CancellationToken>()))
            .Callback<PaymentProviderEvent, CancellationToken>((paymentEvent, _) => onAdd?.Invoke(paymentEvent))
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
