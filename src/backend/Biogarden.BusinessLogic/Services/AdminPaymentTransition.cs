using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.Services;

public static class AdminPaymentTransition
{
    public static void Validate(Order order, PaymentStatus next)
    {
        if (!Enum.IsDefined(next)) throw new ArgumentException("Érvénytelen fizetési állapot.");
        if (next == order.PaymentStatus) return;
        var allowed = order.PaymentStatus switch
        {
            PaymentStatus.Pending or PaymentStatus.Authorized or PaymentStatus.Failed =>
                next == PaymentStatus.Paid && order.PaymentMethod != OrderPaymentMethod.OnlineCard && order.Status != OrderStatus.Cancelled,
            PaymentStatus.Paid => next == PaymentStatus.RefundPending,
            PaymentStatus.RefundPending => next is PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded,
            PaymentStatus.PartiallyRefunded => next is PaymentStatus.RefundPending or PaymentStatus.Refunded,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException("Ez a kézi fizetési állapotváltás nem engedélyezett. Az online fizetést a szolgáltató igazolja; rögzített fizetés nem állítható vissza fizetetlenre.");
    }
}
