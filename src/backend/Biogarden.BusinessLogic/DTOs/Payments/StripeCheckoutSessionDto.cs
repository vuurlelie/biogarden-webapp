namespace Biogarden.BusinessLogic.DTOs.Payments;

public sealed class StripeCheckoutSessionDto
{
    public Guid OrderId { get; init; }
    public string SessionId { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}
