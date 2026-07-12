using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Models.Payments;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Enums;
using Microsoft.Extensions.Options;
using Moq;
using Stripe.Checkout;

namespace Biogarden.UnitTests.Services;

public sealed class StripeCheckoutServiceTests
{
    private readonly Mock<IStripeCheckoutSessionClient> _sessionClient = new(MockBehavior.Strict);

    [Fact]
    public async Task CreateCheckoutSessionAsync_WhenEnabled_BuildsStripeCheckoutSession()
    {
        var order = CreateOrder();
        SessionCreateOptions? capturedOptions = null;

        _sessionClient
            .Setup(client => client.CreateAsync(
                It.IsAny<SessionCreateOptions>(),
                "sk_test_123",
                It.IsAny<CancellationToken>()))
            .Callback<SessionCreateOptions, string, CancellationToken>((options, _, _) => capturedOptions = options)
            .ReturnsAsync(new StripeCheckoutSessionResult(
                "cs_test_123",
                "https://checkout.stripe.com/c/pay/cs_test_123"));

        var service = CreateService(new StripeOptions
        {
            Enabled = true,
            PublishableKey = "pk_test_123",
            SecretKey = "sk_test_123",
            WebhookSecret = "whsec_123",
            Currency = "huf",
            MinorUnitMultiplier = 100,
            SuccessUrl = "https://bakonykincse.hu/rendeles-sikeres?stripe_session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = "https://bakonykincse.hu/penztar?stripe=cancelled",
            Locale = "hu"
        });

        var result = await service.CreateCheckoutSessionAsync(order);

        Assert.Equal("cs_test_123", result.SessionId);
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_test_123", result.Url);

        Assert.NotNull(capturedOptions);
        Assert.Equal("payment", capturedOptions.Mode);
        Assert.Equal(order.Id.ToString("N"), capturedOptions.ClientReferenceId);
        Assert.Equal(order.CustomerEmail, capturedOptions.CustomerEmail);
        Assert.Equal("hu", capturedOptions.Locale);
        Assert.Contains("stripe_session_id={CHECKOUT_SESSION_ID}", capturedOptions.SuccessUrl);
        Assert.Contains($"orderId={order.Id:N}", capturedOptions.SuccessUrl);
        Assert.Contains($"orderId={order.Id:N}", capturedOptions.CancelUrl);

        var lineItem = Assert.Single(capturedOptions.LineItems);
        Assert.Equal(2, lineItem.Quantity);
        Assert.Equal("huf", lineItem.PriceData.Currency);
        Assert.Equal(180000, lineItem.PriceData.UnitAmount);
        Assert.Equal("Levendula", lineItem.PriceData.ProductData.Name);
        Assert.Equal("1", lineItem.PriceData.ProductData.Metadata["productId"]);
        Assert.Equal(order.Id.ToString("N"), capturedOptions.Metadata["orderId"]);
        Assert.Equal(order.OrderNumber, capturedOptions.PaymentIntentData.Metadata["orderNumber"]);
        Assert.Equal(order.CustomerEmail, capturedOptions.PaymentIntentData.ReceiptEmail);

        _sessionClient.VerifyAll();
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_WhenStripeIsDisabled_ThrowsWithoutCallingStripe()
    {
        var service = CreateService(new StripeOptions { Enabled = false });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateCheckoutSessionAsync(CreateOrder()));

        Assert.Equal("Stripe checkout is not enabled.", exception.Message);
        _sessionClient.Verify(
            client => client.CreateAsync(
                It.IsAny<SessionCreateOptions>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_WhenAmountHasFractionalMinorUnits_Throws()
    {
        var order = CreateOrder();
        order.Items[0].UnitPrice = 1800.123m;
        var service = CreateService(new StripeOptions
        {
            Enabled = true,
            SecretKey = "sk_test_123"
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateCheckoutSessionAsync(order));

        Assert.Equal("Stripe amount must resolve to a whole number of minor units.", exception.Message);
    }

    [Fact]
    public void ValidateForStartup_WhenEnabledWithoutSecretKey_Throws()
    {
        var options = new StripeOptions
        {
            Enabled = true,
            PublishableKey = "pk_test_123",
            SecretKey = string.Empty,
            WebhookSecret = "whsec_123"
        };

        var exception = Assert.Throws<InvalidOperationException>(
            options.ValidateForStartup);

        Assert.Equal("Stripe is enabled, but Stripe:SecretKey is not configured.", exception.Message);
    }

    private StripeCheckoutService CreateService(StripeOptions options)
    {
        return new StripeCheckoutService(
            Options.Create(options),
            _sessionClient.Object);
    }

    private static OrderDto CreateOrder()
    {
        return new OrderDto
        {
            Id = Guid.NewGuid(),
            OrderNumber = "BK-TEST-0001",
            CustomerEmail = "customer@example.com",
            TotalPrice = 3600,
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            FulfillmentMethod = FulfillmentMethod.PersonalPickup,
            Items =
            [
                new OrderItemDto
                {
                    ProductId = 1,
                    ProductName = "Levendula",
                    Quantity = 2,
                    Unit = UnitOfMeasure.Piece,
                    UnitPrice = 1800,
                    LineTotal = 3600
                }
            ]
        };
    }
}
