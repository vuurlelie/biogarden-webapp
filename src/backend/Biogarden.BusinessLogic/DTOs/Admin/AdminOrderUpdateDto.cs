using System.ComponentModel.DataAnnotations;
using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Admin;

public sealed class AdminOrderUpdateDto
{
    [EnumDataType(typeof(OrderStatus))]
    public OrderStatus Status { get; set; }

    [EnumDataType(typeof(PaymentStatus))]
    public PaymentStatus PaymentStatus { get; set; }

    [StringLength(1000)]
    public string? CancellationReason { get; set; }
}
