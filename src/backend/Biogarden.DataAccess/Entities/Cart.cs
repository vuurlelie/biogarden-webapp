namespace Biogarden.DataAccess.Entities;

public class Cart
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public string? SessionId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(30);

    public bool IsCheckedOut { get; set; }
    public DateTime? CheckedOutAtUtc { get; set; }

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
