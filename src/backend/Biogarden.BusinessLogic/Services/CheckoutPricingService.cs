using System.Security.Cryptography;
using System.Text.Json;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess.Enums;
using Microsoft.Extensions.Options;

namespace Biogarden.BusinessLogic.Services;

public sealed class CheckoutPricingService(IOptions<CheckoutPricingOptions> options)
{
    private readonly CheckoutPricingOptions _options = options.Value;

    public IReadOnlyList<CheckoutMethodDto> GetMethods() => _options.DeliveryFees
        .OrderBy(deliveryFee => deliveryFee.Key)
        .Select(deliveryFee => new CheckoutMethodDto(deliveryFee.Key, deliveryFee.Value, _options.EnabledPaymentMethods.Where(payment => Allowed(deliveryFee.Key, payment)).ToArray()))
        .Where(checkoutMethod => checkoutMethod.PaymentMethods.Length > 0).ToArray();

    public CheckoutQuoteDto Calculate(Guid cartId, FulfillmentMethod method, OrderPaymentMethod payment, IEnumerable<OrderItemDto> source, GlsDeliveryPointDto? deliveryPoint = null)
    {
        if (!_options.DeliveryFees.TryGetValue(method, out var deliveryFee) ||
            !_options.EnabledPaymentMethods.Contains(payment) || !Allowed(method, payment))
        {
            throw new InvalidOperationException("Ez az átvételi és fizetési mód nem választható.");
        }

        var items = source.OrderBy(orderItem => orderItem.ProductId).ToArray();
        if (items.Length == 0 || items.Any(orderItem => orderItem.Quantity <= 0 || orderItem.UnitPrice < 0))
            throw new InvalidOperationException("A kosár nem árazható.");
        // HUF checkout uses whole forints, rounded once per line, away from zero.
        foreach (var item in items)
            item.LineTotal = CalculateLineTotal(item.UnitPrice, item.Quantity);
        var subtotal = items.Sum(orderItem => orderItem.LineTotal);
        var paymentFee = 0m;
        if (payment == OrderPaymentMethod.CashOnDelivery)
        {
            // The collected amount includes delivery and the COD fee itself.
            paymentFee = subtotal + deliveryFee + _options.CodLowerFee <= _options.CodLowerLimit
                ? _options.CodLowerFee : _options.CodUpperFee;
            if (subtotal + deliveryFee + paymentFee > _options.CodMaximum)
                throw new InvalidOperationException("Az utánvétes összeg meghaladja a megengedett határt.");
        }
        var total = subtotal + deliveryFee + paymentFee;
        // An optimistic comparison token, not a price supplied or trusted by the client.
        var version = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { cartId, method, payment, _options.Currency, subtotal, deliveryFee, paymentFee, items, deliveryPoint })));
        return new(cartId, method, payment, _options.Currency, subtotal, deliveryFee, paymentFee, total, version, items, deliveryPoint);
    }

    public static decimal CalculateLineTotal(decimal unitPrice, int quantity)
        => decimal.Round(unitPrice * quantity, 0, MidpointRounding.AwayFromZero);

    private static bool Allowed(FulfillmentMethod method, OrderPaymentMethod payment) => payment switch
    {
        OrderPaymentMethod.OnlineCard => true,
        OrderPaymentMethod.PayAtPickup => method == FulfillmentMethod.PersonalPickup,
        OrderPaymentMethod.CashOnDelivery => method != FulfillmentMethod.PersonalPickup,
        _ => false
    };
}
