using Biogarden.DataAccess.Enums;

namespace Biogarden.DataAccess.Entities;

public class Order
{
    public Guid Id { get; set; }

    public required string OrderNumber { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public int AddressId { get; set; }
    public Address? Address { get; set; }

    public Guid CartId { get; set; }
    public Cart? Cart { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    public decimal TotalPrice { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public FulfillmentMethod FulfillmentMethod { get; set; } = FulfillmentMethod.PersonalPickup;

    public string? CancellationReason { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
