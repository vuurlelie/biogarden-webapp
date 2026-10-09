using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Admin;

public sealed class AdminOrderWithdrawalRequestDto
{
    public int Id { get; init; }
    public Guid OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public OrderStatus OrderStatus { get; init; }
    public decimal OrderTotalPrice { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public OrderWithdrawalRequestStatus Status { get; init; }
    public DateTime RequestedAtUtc { get; init; }
    public string? AdminResponse { get; init; }
    public DateTime? HandledAtUtc { get; init; }
}
