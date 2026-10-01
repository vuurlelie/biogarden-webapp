using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Admin;

public sealed class AdminOrderUpdateDto
{
    public string? Version { get; set; }
    public OrderStatus Status { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public string? CancellationReason { get; set; }
}
