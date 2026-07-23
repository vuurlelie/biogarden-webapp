using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Payments;

public sealed class PublicOrderPaymentStatusDto
{
    public Guid OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public DateTime? ConfirmedAtUtc { get; set; }
}
