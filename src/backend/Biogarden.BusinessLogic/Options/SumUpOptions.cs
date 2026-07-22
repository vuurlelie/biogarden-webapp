namespace Biogarden.BusinessLogic.Options;

public sealed class SumUpOptions
{
    public const string SectionName = "SumUp";

    private const string CheckoutReferencePlaceholder = "{CHECKOUT_REFERENCE}";

    public bool Enabled { get; init; }
    public string ApiKey { get; init; } = string.Empty;
    public string MerchantCode { get; init; } = string.Empty;
    public string Currency { get; init; } = "HUF";
    public string ApiBaseUrl { get; init; } = "https://api.sumup.com";
    public string RedirectUrl { get; init; } = "http://localhost:5173/rendeles-sikeres?sumup_checkout_reference={CHECKOUT_REFERENCE}";
    public string ReturnUrl { get; init; } = "http://localhost:5097/api/sumup/checkout-status";

    public void ValidateForStartup()
    {
        if (string.IsNullOrWhiteSpace(Currency) || Currency.Length != 3)
        {
            throw new InvalidOperationException(
                "SumUp:Currency must be a three-letter ISO currency code.");
        }

        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var apiBaseUri) ||
            (apiBaseUri.Scheme != Uri.UriSchemeHttp && apiBaseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "SumUp:ApiBaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (!IsAbsoluteHttpUrl(RedirectUrl))
        {
            throw new InvalidOperationException(
                "SumUp:RedirectUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (!RedirectUrl.Contains(CheckoutReferencePlaceholder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"SumUp:RedirectUrl must contain the {CheckoutReferencePlaceholder} placeholder.");
        }

        if (!IsAbsoluteHttpUrl(ReturnUrl))
        {
            throw new InvalidOperationException(
                "SumUp:ReturnUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException(
                "SumUp is enabled, but SumUp:ApiKey is not configured.");
        }

        if (string.IsNullOrWhiteSpace(MerchantCode))
        {
            throw new InvalidOperationException(
                "SumUp is enabled, but SumUp:MerchantCode is not configured.");
        }
    }

    private static bool IsAbsoluteHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
