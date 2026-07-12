using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Models.Payments;
using Biogarden.BusinessLogic.Options;
using Microsoft.Extensions.Options;
using Stripe.Checkout;

namespace Biogarden.BusinessLogic.Services;

public sealed class StripeCheckoutService : IStripeCheckoutService
{
    private const string CheckoutSessionPlaceholder = "{CHECKOUT_SESSION_ID}";

    private readonly StripeOptions _options;
    private readonly IStripeCheckoutSessionClient _sessionClient;

    public StripeCheckoutService(
        IOptions<StripeOptions> options,
        IStripeCheckoutSessionClient sessionClient)
    {
        _options = options.Value;
        _sessionClient = sessionClient;
    }

    public Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
        OrderDto order,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Stripe checkout is not enabled.");
        }

        if (order.Items.Count == 0)
        {
            throw new InvalidOperationException("Cannot create a Stripe checkout session for an empty order.");
        }

        var metadata = new Dictionary<string, string>
        {
            ["orderId"] = order.Id.ToString("N"),
            ["orderNumber"] = order.OrderNumber
        };

        var sessionOptions = new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = order.Id.ToString("N"),
            CustomerEmail = order.CustomerEmail,
            SuccessUrl = BuildSuccessUrl(order),
            CancelUrl = BuildCancelUrl(order),
            Locale = _options.Locale,
            Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                ReceiptEmail = order.CustomerEmail,
                Metadata = metadata
            },
            LineItems = order.Items.Select(item => new SessionLineItemOptions
            {
                Quantity = item.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = _options.Currency.ToLowerInvariant(),
                    UnitAmount = ToStripeAmount(item.UnitPrice),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = item.ProductName,
                        Metadata = new Dictionary<string, string>
                        {
                            ["productId"] = item.ProductId.ToString()
                        }
                    }
                }
            }).ToList()
        };

        return _sessionClient.CreateAsync(
            sessionOptions,
            _options.SecretKey,
            cancellationToken);
    }

    private string BuildSuccessUrl(OrderDto order)
    {
        return AppendOrderQuery(_options.SuccessUrl, order);
    }

    private string BuildCancelUrl(OrderDto order)
    {
        return AppendOrderQuery(_options.CancelUrl, order);
    }

    private static string AppendOrderQuery(string url, OrderDto order)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}orderId={Uri.EscapeDataString(order.Id.ToString("N"))}";
    }

    private long ToStripeAmount(decimal amount)
    {
        var minorUnits = amount * _options.MinorUnitMultiplier;

        if (minorUnits != decimal.Truncate(minorUnits))
        {
            throw new InvalidOperationException(
                "Stripe amount must resolve to a whole number of minor units.");
        }

        return decimal.ToInt64(minorUnits);
    }
}
