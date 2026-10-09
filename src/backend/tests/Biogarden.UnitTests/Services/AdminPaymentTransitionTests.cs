using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;

namespace Biogarden.UnitTests.Services;

public sealed class AdminPaymentTransitionTests
{
    [Fact]
    public void UndefinedPaymentStatusIsRejectedWithoutChangingTheOrder()
    {
        var order = new Order { OrderNumber = "payment-transition-test", PaymentStatus = PaymentStatus.Pending };

        Assert.Throws<ArgumentException>(() => AdminPaymentTransition.Validate(order, (PaymentStatus)999));

        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Authorized, PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Failed, PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.RefundPending)]
    [InlineData(PaymentStatus.RefundPending, PaymentStatus.PartiallyRefunded)]
    [InlineData(PaymentStatus.RefundPending, PaymentStatus.Refunded)]
    [InlineData(PaymentStatus.PartiallyRefunded, PaymentStatus.RefundPending)]
    [InlineData(PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded)]
    [InlineData(PaymentStatus.Refunded, PaymentStatus.Refunded)]
    public void OfflinePaymentAndRefundTransitionsAreAllowed(PaymentStatus current, PaymentStatus next)
    {
        var order = new Order { OrderNumber = "payment-transition-test", PaymentStatus = current, PaymentMethod = OrderPaymentMethod.PayAtPickup };

        AdminPaymentTransition.Validate(order, next);

        Assert.Equal(current, order.PaymentStatus);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Paid, OrderPaymentMethod.OnlineCard, OrderStatus.Pending)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Paid, OrderPaymentMethod.PayAtPickup, OrderStatus.Cancelled)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Pending, OrderPaymentMethod.PayAtPickup, OrderStatus.Confirmed)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Failed, OrderPaymentMethod.PayAtPickup, OrderStatus.Confirmed)]
    [InlineData(PaymentStatus.RefundPending, PaymentStatus.Paid, OrderPaymentMethod.PayAtPickup, OrderStatus.Completed)]
    [InlineData(PaymentStatus.PartiallyRefunded, PaymentStatus.Failed, OrderPaymentMethod.PayAtPickup, OrderStatus.Completed)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Refunded, OrderPaymentMethod.PayAtPickup, OrderStatus.Pending)]
    [InlineData(PaymentStatus.Refunded, PaymentStatus.Paid, OrderPaymentMethod.PayAtPickup, OrderStatus.Completed)]
    public void UnsafeManualTransitionsAreRejected(PaymentStatus current, PaymentStatus next, OrderPaymentMethod method, OrderStatus status)
    {
        var order = new Order { OrderNumber = "payment-transition-test", PaymentStatus = current, PaymentMethod = method, Status = status };

        Assert.Throws<InvalidOperationException>(() => AdminPaymentTransition.Validate(order, next));

        Assert.Equal(current, order.PaymentStatus);
    }
}
