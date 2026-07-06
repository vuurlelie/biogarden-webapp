using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Orders;

public class OrderDto
{
    public Guid Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public int AddressId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    public decimal TotalPrice { get; set; }

    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }

    public FulfillmentMethod FulfillmentMethod { get; set; }

    public string? CancellationReason { get; set; }

    public List<OrderItemDto> Items { get; set; } = [];
}
