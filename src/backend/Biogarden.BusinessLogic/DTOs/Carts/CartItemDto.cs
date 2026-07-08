namespace Biogarden.BusinessLogic.DTOs.Carts;

public class CartItemDto
{
    public Guid Id { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public string? Image { get; set; }

    public global::UnitOfMeasure Unit { get; set; }

    public int Quantity { get; set; }

    public int StockQuantity { get; set; }

    public int ReservedQuantityExcludingCurrentCart { get; set; }

    public int AvailableQuantityForCart { get; set; }

    public bool Available { get; set; }

    public bool DeliveryAvailable { get; set; }

    public decimal LineTotal { get; set; }
}
