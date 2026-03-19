using Biogarden.DataAccess.Enums;

namespace Biogarden.DataAccess.Entities;

public class Order
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public Guid AddressId { get; set; }
    public Address? Address { get; set; }

    public Guid CartId { get; set; }
    public Cart? Cart { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public decimal TotalPrice { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}