using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;

namespace Biogarden.UnitTests.Services;

public sealed class AdminPaymentTransitionTests
{
    [Theory]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Failed, PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.RefundPending)]
    [InlineData(PaymentStatus.RefundPending, PaymentStatus.PartiallyRefunded)]
    [InlineData(PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded)]
    [InlineData(PaymentStatus.RefundPending, PaymentStatus.Refunded)]
    public void ValidOfflineAndRefundTransitionsAreAccepted(PaymentStatus before, PaymentStatus after)
        => AdminPaymentTransition.Validate(new Order { OrderNumber = "Test", PaymentStatus = before, PaymentMethod = OrderPaymentMethod.PayAtPickup }, after);

    [Theory]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Failed)]
    [InlineData(PaymentStatus.RefundPending, PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Refunded, PaymentStatus.Paid)]
    [InlineData(PaymentStatus.PartiallyRefunded, PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Refunded)]
    public void InvalidCorrectionsAreRejected(PaymentStatus before, PaymentStatus after)
        => Assert.Throws<InvalidOperationException>(() => AdminPaymentTransition.Validate(new Order { OrderNumber = "Test", PaymentStatus = before }, after));

    [Fact]
    public void OnlineSuccessAndCancelledUnpaidOrderCannotBeManuallyMarkedPaid()
    {
        Assert.Throws<InvalidOperationException>(() => AdminPaymentTransition.Validate(new Order { OrderNumber = "Test", PaymentMethod = OrderPaymentMethod.OnlineCard }, PaymentStatus.Paid));
        Assert.Throws<InvalidOperationException>(() => AdminPaymentTransition.Validate(new Order { OrderNumber = "Test", Status = OrderStatus.Cancelled }, PaymentStatus.Paid));
    }
}
