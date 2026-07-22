namespace Biogarden.BusinessLogic.DTOs.Payments;

public sealed class SumUpCheckoutDto
{
    public Guid OrderId { get; init; }
    public string CheckoutId { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}
