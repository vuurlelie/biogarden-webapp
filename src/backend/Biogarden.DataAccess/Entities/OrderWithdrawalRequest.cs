using Biogarden.DataAccess.Enums;

namespace Biogarden.DataAccess.Entities;

public sealed class OrderWithdrawalRequest
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid? UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public OrderWithdrawalRequestStatus Status { get; set; } = OrderWithdrawalRequestStatus.Pending;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public string? AdminResponse { get; set; }
    public DateTime? HandledAtUtc { get; set; }
}
