using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Enums;
using Microsoft.Extensions.Options;

namespace Biogarden.UnitTests.Services;

public sealed class CheckoutPricingServiceTests
{
    private static CheckoutPricingOptions Configuration() => new()
    {
        DeliveryFees = new()
        {
            [FulfillmentMethod.PersonalPickup] = 0,
            [FulfillmentMethod.HomeDelivery] = 2740,
            [FulfillmentMethod.ParcelLocker] = 2090,
            [FulfillmentMethod.ParcelShop] = 2590
        },
        EnabledPaymentMethods = Enum.GetValues<OrderPaymentMethod>(),
        CodLowerFee = 600,
        CodUpperFee = 1190,
        CodLowerLimit = 30000,
        CodMaximum = 499999
    };
    private static CheckoutQuoteDto Quote(decimal price, FulfillmentMethod method, OrderPaymentMethod payment)
        => new CheckoutPricingService(Options.Create(Configuration())).Calculate(Guid.Empty, method, payment,
            [new() { ProductId = 1, ProductName = "Tea", UnitPrice = price, Quantity = 1 }]);

    [Theory]
    [InlineData(FulfillmentMethod.PersonalPickup, OrderPaymentMethod.PayAtPickup, 0)]
    [InlineData(FulfillmentMethod.PersonalPickup, OrderPaymentMethod.OnlineCard, 0)]
    [InlineData(FulfillmentMethod.HomeDelivery, OrderPaymentMethod.OnlineCard, 2740)]
    [InlineData(FulfillmentMethod.ParcelLocker, OrderPaymentMethod.OnlineCard, 2090)]
    [InlineData(FulfillmentMethod.ParcelShop, OrderPaymentMethod.OnlineCard, 2590)]
    public void QuoteUsesConfiguredFee(FulfillmentMethod method, OrderPaymentMethod payment, decimal fee)
    {
        var quote = Quote(5000, method, payment);
        Assert.Equal(5000, quote.Subtotal);
        Assert.Equal(fee, quote.DeliveryFee);
        Assert.Equal(5000 + fee, quote.TotalPrice);
        Assert.Equal("HUF", quote.Currency);
    }

    [Theory]
    [InlineData(26660, 600, 30000)]
    [InlineData(26661, 1190, 30591)]
    [InlineData(496069, 1190, 499999)]
    public void CodTierIncludesDeliveryAndCollectionFee(decimal subtotal, decimal fee, decimal total)
    {
        var quote = Quote(subtotal, FulfillmentMethod.HomeDelivery, OrderPaymentMethod.CashOnDelivery);
        Assert.Equal(fee, quote.PaymentFee);
        Assert.Equal(total, quote.TotalPrice);
    }

    [Fact]
    public void CodOverMaximumIsRejected() => Assert.Throws<InvalidOperationException>(() =>
        Quote(496070, FulfillmentMethod.HomeDelivery, OrderPaymentMethod.CashOnDelivery));

    [Theory]
    [InlineData(FulfillmentMethod.PersonalPickup, OrderPaymentMethod.CashOnDelivery)]
    [InlineData(FulfillmentMethod.HomeDelivery, OrderPaymentMethod.PayAtPickup)]
    [InlineData((FulfillmentMethod)99, OrderPaymentMethod.OnlineCard)]
    [InlineData(FulfillmentMethod.HomeDelivery, (OrderPaymentMethod)99)]
    public void InvalidCombinationIsRejected(FulfillmentMethod method, OrderPaymentMethod payment)
        => Assert.Throws<InvalidOperationException>(() => Quote(5000, method, payment));

    [Fact]
    public void RoundsPerLineAndVersionChangesEvenWhenTotalStaysTheSame()
    {
        var service = new CheckoutPricingService(Options.Create(Configuration()));
        var first = service.Calculate(Guid.Empty, FulfillmentMethod.PersonalPickup, OrderPaymentMethod.OnlineCard,
            [new() { ProductId = 1, UnitPrice = 10.25m, Quantity = 2 }]);
        var second = service.Calculate(Guid.Empty, FulfillmentMethod.PersonalPickup, OrderPaymentMethod.OnlineCard,
            [new() { ProductId = 1, UnitPrice = 10.3m, Quantity = 2 }]);
        Assert.Equal(21, first.TotalPrice);
        Assert.Equal(first.TotalPrice, second.TotalPrice);
        Assert.NotEqual(first.Version, second.Version);
    }

    [Fact]
    public void DisabledMethodsAreNotOfferedOrAccepted()
    {
        var service = new CheckoutPricingService(Options.Create(new CheckoutPricingOptions
        {
            DeliveryFees = new() { [FulfillmentMethod.PersonalPickup] = 0 },
            EnabledPaymentMethods = [OrderPaymentMethod.PayAtPickup]
        }));
        Assert.Single(service.GetMethods());
        Assert.Throws<InvalidOperationException>(() => service.Calculate(Guid.Empty,
            FulfillmentMethod.PersonalPickup, OrderPaymentMethod.OnlineCard,
            [new() { ProductId = 1, UnitPrice = 10, Quantity = 1 }]));
    }

    [Fact]
    public void ConfigurationChangeInvalidatesTheReviewedQuote()
    {
        var config = Configuration();
        var service = new CheckoutPricingService(Options.Create(config));
        OrderItemDto[] items = [new() { ProductId = 1, UnitPrice = 5000, Quantity = 1 }];
        var before = service.Calculate(Guid.Empty, FulfillmentMethod.HomeDelivery, OrderPaymentMethod.OnlineCard, items);
        config.DeliveryFees[FulfillmentMethod.HomeDelivery] = 2800;
        var after = service.Calculate(Guid.Empty, FulfillmentMethod.HomeDelivery, OrderPaymentMethod.OnlineCard, items);
        Assert.NotEqual(before.Version, after.Version);
        Assert.Equal(7800, after.TotalPrice);
        Assert.Equal(7740, before.TotalPrice);
    }

    [Fact]
    public void StartupRejectsInvalidFeesAndCurrency()
    {
        Configuration().ValidateForStartup();
        Assert.Throws<InvalidOperationException>(() => new CheckoutPricingOptions().ValidateForStartup());
        Assert.Throws<InvalidOperationException>(() => new CheckoutPricingOptions
        {
            DeliveryFees = new() { [FulfillmentMethod.PersonalPickup] = -1 },
            EnabledPaymentMethods = [OrderPaymentMethod.PayAtPickup]
        }.ValidateForStartup());
        Assert.Throws<InvalidOperationException>(() => new CheckoutPricingOptions
        {
            Currency = "EUR",
            DeliveryFees = new() { [FulfillmentMethod.PersonalPickup] = 0 },
            EnabledPaymentMethods = [OrderPaymentMethod.PayAtPickup]
        }.ValidateForStartup());
    }

    [Theory]
    [InlineData(FulfillmentMethod.HomeDelivery, 2740)]
    [InlineData(FulfillmentMethod.ParcelLocker, 2090)]
    [InlineData(FulfillmentMethod.ParcelShop, 2590)]
    public void CodBoundariesUseRoundedCollectedAmountForEveryDeliveryMethod(FulfillmentMethod method, decimal delivery)
    {
        var lastLowerSubtotal = 30000 - delivery - 600;
        var belowMidpoint = Quote(lastLowerSubtotal + 0.49m, method, OrderPaymentMethod.CashOnDelivery);
        Assert.Equal(600, belowMidpoint.PaymentFee);
        Assert.Equal(30000, belowMidpoint.TotalPrice);
        var atMidpoint = Quote(lastLowerSubtotal + 0.50m, method, OrderPaymentMethod.CashOnDelivery);
        Assert.Equal(1190, atMidpoint.PaymentFee);
        Assert.Equal(30591, atMidpoint.TotalPrice);
        var lastUpperSubtotal = 499999 - delivery - 1190;
        Assert.Equal(499999, Quote(lastUpperSubtotal + 0.49m, method, OrderPaymentMethod.CashOnDelivery).TotalPrice);
        Assert.Throws<InvalidOperationException>(() => Quote(lastUpperSubtotal + 0.50m, method, OrderPaymentMethod.CashOnDelivery));
    }

    [Fact]
    public void MultipleLinesRoundIndividuallyAndInputOrderDoesNotChangeVersion()
    {
        var service = new CheckoutPricingService(Options.Create(Configuration()));
        OrderItemDto[] items = [new() { ProductId = 2, UnitPrice = 10.25m, Quantity = 2 },
            new() { ProductId = 1, UnitPrice = 10.49m, Quantity = 1 }];
        var first = service.Calculate(Guid.Empty, FulfillmentMethod.PersonalPickup, OrderPaymentMethod.OnlineCard, items);
        var reordered = service.Calculate(Guid.Empty, FulfillmentMethod.PersonalPickup, OrderPaymentMethod.OnlineCard, items.Reverse());
        Assert.Equal(31, first.Subtotal);
        Assert.Equal(new decimal[] { 10, 21 }, first.Items.Select(orderItem => orderItem.LineTotal));
        Assert.Equal(first.Version, reordered.Version);
    }
}
