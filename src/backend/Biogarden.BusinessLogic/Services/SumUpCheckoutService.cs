using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Models.Payments;
using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess.Enums;
using Microsoft.Extensions.Options;

namespace Biogarden.BusinessLogic.Services;

public sealed class SumUpCheckoutService : ISumUpCheckoutService
{
    private const string CheckoutReferencePlaceholder = "{CHECKOUT_REFERENCE}";

    private readonly SumUpOptions _options;
    private readonly ISumUpCheckoutClient _checkoutClient;

    public SumUpCheckoutService(
        IOptions<SumUpOptions> options,
        ISumUpCheckoutClient checkoutClient)
    {
        _options = options.Value;
        _checkoutClient = checkoutClient;
    }

    public async Task<SumUpCheckoutResult> CreateCheckoutAsync(
        OrderDto order,
        CancellationToken cancellationToken = default, string? reference = null)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("SumUp checkout is not enabled.");
        }

        if (order.Items.Count == 0)
        {
            throw new InvalidOperationException("Cannot create a SumUp checkout for an empty order.");
        }

        if (order.PaymentMethod is not null and not OrderPaymentMethod.OnlineCard)
            throw new InvalidOperationException("Ehhez a rendeléshez nem online bankkártyás fizetést választottál.");
        if (order.Currency is not null && !string.Equals(order.Currency, _options.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A rendelés pénzneme nem egyezik a fizetési szolgáltatás beállításával.");

        var checkoutReference = order.Id.ToString("N");
        var provisionalRedirectUrl = BuildRedirectUrl(checkoutReference, order);

        var result = await _checkoutClient.CreateAsync(
            new SumUpCreateCheckoutRequest(
                CheckoutReference: reference ?? checkoutReference,
                Amount: order.TotalPrice,
                Currency: (order.Currency ?? _options.Currency).ToUpperInvariant(),
                MerchantCode: _options.MerchantCode,
                Description: $"Bakony Kincse rendelés {order.OrderNumber}",
                RedirectUrl: provisionalRedirectUrl,
                ReturnUrl: _options.ReturnUrl),
            _options.ApiKey,
            BuildApiBaseUri(),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(result.HostedCheckoutUrl))
        {
            throw new InvalidOperationException("SumUp checkout response did not include a hosted checkout URL.");
        }

        return result;
    }

    public Task<SumUpCheckoutResult> GetCheckoutAsync(
        string checkoutId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("SumUp checkout is not enabled.");
        }

        if (string.IsNullOrWhiteSpace(checkoutId))
        {
            throw new ArgumentException("SumUp checkout id is required.", nameof(checkoutId));
        }

        return _checkoutClient.GetAsync(
            checkoutId.Trim(),
            _options.ApiKey,
            BuildApiBaseUri(),
            cancellationToken);
    }

    public Task<IReadOnlyList<SumUpCheckoutResult>> FindCheckoutsAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) throw new InvalidOperationException("SumUp checkout is not enabled.");
        return _checkoutClient.FindAsync(reference, _options.ApiKey, BuildApiBaseUri(), cancellationToken);
    }

    private string BuildRedirectUrl(string checkoutId, OrderDto order)
    {
        var url = _options.RedirectUrl.Replace(
            CheckoutReferencePlaceholder,
            Uri.EscapeDataString(checkoutId),
            StringComparison.Ordinal);

        return AppendOrderQuery(url, order);
    }

    private static string AppendOrderQuery(string url, OrderDto order)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}orderId={Uri.EscapeDataString(order.Id.ToString("N"))}";
    }

    private Uri BuildApiBaseUri()
    {
        return new Uri(_options.ApiBaseUrl.TrimEnd('/'), UriKind.Absolute);
    }
}
