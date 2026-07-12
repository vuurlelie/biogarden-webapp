namespace Biogarden.BusinessLogic.Models.Payments;

public sealed record StripeCheckoutSessionResult(
    string SessionId,
    string Url,
    string? PaymentIntentId = null);
