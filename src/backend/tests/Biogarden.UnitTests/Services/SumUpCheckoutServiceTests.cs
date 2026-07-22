using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Models.Payments;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Enums;
using Microsoft.Extensions.Options;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class SumUpCheckoutServiceTests
{
    private readonly Mock<ISumUpCheckoutClient> _checkoutClient = new(MockBehavior.Strict);

    [Fact]
    public async Task CreateCheckoutAsync_WhenEnabled_BuildsHostedSumUpCheckout()
    {
        var order = CreateOrder();
        SumUpCreateCheckoutRequest? capturedRequest = null;

        _checkoutClient
            .Setup(client => client.CreateAsync(
                It.IsAny<SumUpCreateCheckoutRequest>(),
                "sumup_api_key",
                It.Is<Uri>(uri => uri.ToString() == "https://api.sumup.com/"),
                It.IsAny<CancellationToken>()))
            .Callback<SumUpCreateCheckoutRequest, string, Uri, CancellationToken>((request, _, _, _) => capturedRequest = request)
            .ReturnsAsync(new SumUpCheckoutResult(
                "checkout_123",
                "PENDING",
                "https://checkout.sumup.com/pay/checkout_123"));

        var service = CreateService(new SumUpOptions
        {
            Enabled = true,
            ApiKey = "sumup_api_key",
            MerchantCode = "MC_TEST",
            Currency = "HUF",
            RedirectUrl = "https://bakonykincse.hu/rendeles-sikeres?sumup_checkout_reference={CHECKOUT_REFERENCE}",
            ReturnUrl = "https://api.bakonykincse.hu/api/sumup/checkout-status"
        });

        var result = await service.CreateCheckoutAsync(order);

        Assert.Equal("checkout_123", result.CheckoutId);
        Assert.Equal("https://checkout.sumup.com/pay/checkout_123", result.HostedCheckoutUrl);

        Assert.NotNull(capturedRequest);
        Assert.Equal(order.Id.ToString("N"), capturedRequest.CheckoutReference);
        Assert.Equal(3600, capturedRequest.Amount);
        Assert.Equal("HUF", capturedRequest.Currency);
        Assert.Equal("MC_TEST", capturedRequest.MerchantCode);
        Assert.Equal("Bakony Kincse rendelés BK-TEST-0001", capturedRequest.Description);
        Assert.Contains("sumup_checkout_reference=", capturedRequest.RedirectUrl);
        Assert.Contains($"orderId={order.Id:N}", capturedRequest.RedirectUrl);
        Assert.Equal("https://api.bakonykincse.hu/api/sumup/checkout-status", capturedRequest.ReturnUrl);

        _checkoutClient.VerifyAll();
    }

    [Fact]
    public async Task CreateCheckoutAsync_WhenSumUpIsDisabled_ThrowsWithoutCallingClient()
    {
        var service = CreateService(new SumUpOptions { Enabled = false });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateCheckoutAsync(CreateOrder()));

        Assert.Equal("SumUp checkout is not enabled.", exception.Message);
        _checkoutClient.Verify(
            client => client.CreateAsync(
                It.IsAny<SumUpCreateCheckoutRequest>(),
                It.IsAny<string>(),
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ValidateForStartup_WhenEnabledWithoutApiKey_Throws()
    {
        var options = new SumUpOptions
        {
            Enabled = true,
            ApiKey = string.Empty,
            MerchantCode = "MC_TEST"
        };

        var exception = Assert.Throws<InvalidOperationException>(
            options.ValidateForStartup);

        Assert.Equal("SumUp is enabled, but SumUp:ApiKey is not configured.", exception.Message);
    }

    private SumUpCheckoutService CreateService(SumUpOptions options)
    {
        return new SumUpCheckoutService(
            Options.Create(options),
            _checkoutClient.Object);
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
