namespace Biogarden.BusinessLogic.DTOs.Carts;

public class CartDto
{
    public Guid Id { get; set; }

    public string? UserId { get; set; }

    public string? SessionId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastActivityAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public bool IsCheckedOut { get; set; }

    public int TotalItemCount { get; set; }

    public decimal TotalPrice { get; set; }

    public List<CartItemDto> Items { get; set; } = [];
}