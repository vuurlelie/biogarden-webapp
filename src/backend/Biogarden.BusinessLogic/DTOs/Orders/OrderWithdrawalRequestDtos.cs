using System.ComponentModel.DataAnnotations;
using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Orders;

public sealed class OrderWithdrawalRequestDto
{
    public int Id { get; init; }
    public Guid OrderId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public OrderWithdrawalRequestStatus Status { get; init; }
    public DateTime RequestedAtUtc { get; init; }
    public DateTime? HandledAtUtc { get; init; }
}

public sealed class CreateOrderWithdrawalRequestDto
{
    [Required]
    [MinLength(10)]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}
