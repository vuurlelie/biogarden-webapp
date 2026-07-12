using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Microsoft.Extensions.Options;
using Moq;
using Stripe;

namespace Biogarden.UnitTests.Services;

public sealed class StripeWebhookServiceTests
{
    private const string WebhookSecret = "whsec_test_123";

    private readonly Mock<IOrderService> _orderService = new(MockBehavior.Strict);

    [Fact]
    public async Task HandleAsync_ForCompletedCheckoutSession_ConfirmsOrderPayment()
    {
        var payload = CreateCheckoutSessionEventPayload(
            "evt_completed",
            "checkout.session.completed");
        var signatureHeader = CreateSignatureHeader(payload);
        var service = CreateService();

        _orderService
            .Setup(service => service.ConfirmStripeCheckoutSessionAsync(
                "cs_test_123",
                "pi_test_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderDto());

        await service.HandleAsync(payload, signatureHeader);

        _orderService.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_ForFailedCheckoutSession_MarksOrderPaymentFailed()
    {
        var payload = CreateCheckoutSessionEventPayload(
            "evt_failed",
            "checkout.session.async_payment_failed");
        var signatureHeader = CreateSignatureHeader(payload);
        var service = CreateService();

        _orderService
            .Setup(service => service.MarkStripePaymentFailedAsync(
                "cs_test_123",
                "pi_test_123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderDto());

        await service.HandleAsync(payload, signatureHeader);

        _orderService.VerifyAll();
    }

    private StripeWebhookService CreateService()
    {
        return new StripeWebhookService(
            Options.Create(new StripeOptions { WebhookSecret = WebhookSecret }),
            _orderService.Object);
    }

    private static string CreateSignatureHeader(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = EventUtility.ComputeSignature(WebhookSecret, timestamp, payload);

        return $"t={timestamp},v1={signature}";
    }

    private static string CreateCheckoutSessionEventPayload(string eventId, string eventType)
    {
        return $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2026-06-24.dahlia",
          "created": 1710000000,
          "type": "{{eventType}}",
          "livemode": false,
          "data": {
            "object": {
              "id": "cs_test_123",
              "object": "checkout.session",
              "payment_intent": "pi_test_123"
            }
          }
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
