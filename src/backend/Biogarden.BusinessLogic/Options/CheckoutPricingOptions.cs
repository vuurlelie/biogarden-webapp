using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.Options;

public sealed class CheckoutPricingOptions
{
    public const string SectionName = "CheckoutPricing";
    public const string SupportedCurrency = "HUF";
    public string Currency { get; init; } = SupportedCurrency;
    public Dictionary<FulfillmentMethod, decimal> DeliveryFees { get; init; } = new();
    public OrderPaymentMethod[] EnabledPaymentMethods { get; init; } = [];
    public decimal CodLowerFee { get; init; }
    public decimal CodUpperFee { get; init; }
    public decimal CodLowerLimit { get; init; }
    public decimal CodMaximum { get; init; }

    public void ValidateForStartup()
    {
        if (Currency != SupportedCurrency || DeliveryFees.Count == 0 ||
            DeliveryFees.Any(x => !Enum.IsDefined(x.Key) || !ValidMoney(x.Value)) ||
            EnabledPaymentMethods.Length == 0 || EnabledPaymentMethods.Any(x => !Enum.IsDefined(x)) ||
            !ValidMoney(CodLowerFee) || !ValidMoney(CodUpperFee) || CodUpperFee < CodLowerFee ||
            !ValidMoney(CodLowerLimit) || !ValidMoney(CodMaximum) ||
            (EnabledPaymentMethods.Contains(OrderPaymentMethod.CashOnDelivery) &&
                (CodLowerLimit <= CodLowerFee || CodMaximum <= CodLowerLimit + CodUpperFee)))
            throw new InvalidOperationException("CheckoutPricing configuration is invalid.");
    }

    private static bool ValidMoney(decimal value) => value >= 0 && value <= 999999999m && decimal.Truncate(value) == value;
}
