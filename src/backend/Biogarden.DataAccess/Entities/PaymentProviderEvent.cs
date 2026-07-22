namespace Biogarden.DataAccess.Entities;

public class PaymentProviderEvent
{
    public Guid Id { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string EventKey { get; set; } = string.Empty;

    public string CheckoutId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string? Status { get; set; }

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAtUtc { get; set; }
}
