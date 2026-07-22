using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Models.Payments;
using Biogarden.BusinessLogic.Services;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class SumUpCheckoutStatusServiceTests
{
    private readonly Mock<ISumUpCheckoutService> _checkoutService = new(MockBehavior.Strict);
    private readonly Mock<IOrderService> _orderService = new(MockBehavior.Strict);

    [Fact]
    public async Task HandleAsync_ForPaidCheckout_ConfirmsOrderPayment()
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
                TransactionCode: "txn_123"));
        _orderService
            .Setup(service => service.ConfirmSumUpCheckoutAsync(
                "checkout_123",
                "txn_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderDto());

        await service.HandleAsync(payload);

        _checkoutService.VerifyAll();
        _orderService.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_ForFailedCheckout_MarksOrderPaymentFailed()
    {
        var payload = CreateCheckoutStatusPayload("checkout_123");
        var service = CreateService();

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

        await service.HandleAsync(payload);

        _checkoutService.VerifyAll();
        _orderService.VerifyAll();
    }

    private SumUpCheckoutStatusService CreateService()
    {
        return new SumUpCheckoutStatusService(
            _checkoutService.Object,
            _orderService.Object);
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
