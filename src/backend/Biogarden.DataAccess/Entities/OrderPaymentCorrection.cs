using Biogarden.DataAccess.Enums;

namespace Biogarden.DataAccess.Entities;

public sealed class OrderPaymentCorrection
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    // Keep the actor identifier even if the account is later anonymized or removed.
    public Guid ActorId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public PaymentStatus PreviousStatus { get; set; }
    public PaymentStatus NewStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
}
