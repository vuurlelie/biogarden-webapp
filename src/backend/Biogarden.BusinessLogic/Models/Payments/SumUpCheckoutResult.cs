namespace Biogarden.BusinessLogic.Models.Payments;

public sealed record SumUpCheckoutResult(
    string CheckoutId,
    string Status,
    string? HostedCheckoutUrl = null,
    string? TransactionCode = null,
    decimal? Amount = null,
    string? Currency = null,
    string? CheckoutReference = null,
    string? MerchantCode = null);
