namespace Biogarden.BusinessLogic.Models.Payments;

public sealed record SumUpCreateCheckoutRequest(
    string CheckoutReference,
    decimal Amount,
    string Currency,
    string MerchantCode,
    string Description,
    string RedirectUrl,
    string ReturnUrl);
