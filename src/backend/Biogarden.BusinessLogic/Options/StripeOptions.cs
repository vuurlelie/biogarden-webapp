namespace Biogarden.BusinessLogic.Options;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public bool Enabled { get; init; }
    public string PublishableKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string WebhookSecret { get; init; } = string.Empty;
    public string Currency { get; init; } = "huf";
    public int MinorUnitMultiplier { get; init; } = 100;
    public string SuccessUrl { get; init; } = "http://localhost:5173/rendeles-sikeres?stripe_session_id={CHECKOUT_SESSION_ID}";
    public string CancelUrl { get; init; } = "http://localhost:5173/penztar?stripe=cancelled";
    public string Locale { get; init; } = "hu";

    public void ValidateForStartup()
    {
        if (MinorUnitMultiplier <= 0)
        {
            throw new InvalidOperationException(
                "Stripe:MinorUnitMultiplier must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(Currency) || Currency.Length != 3)
        {
            throw new InvalidOperationException(
                "Stripe:Currency must be a three-letter ISO currency code.");
        }

        if (!IsAbsoluteHttpUrl(SuccessUrl))
        {
            throw new InvalidOperationException(
                "Stripe:SuccessUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (!IsAbsoluteHttpUrl(CancelUrl))
        {
            throw new InvalidOperationException(
                "Stripe:CancelUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(PublishableKey))
        {
            throw new InvalidOperationException(
                "Stripe is enabled, but Stripe:PublishableKey is not configured.");
        }

        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            throw new InvalidOperationException(
                "Stripe is enabled, but Stripe:SecretKey is not configured.");
        }

        if (string.IsNullOrWhiteSpace(WebhookSecret))
        {
            throw new InvalidOperationException(
                "Stripe is enabled, but Stripe:WebhookSecret is not configured.");
        }
    }

    private static bool IsAbsoluteHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
