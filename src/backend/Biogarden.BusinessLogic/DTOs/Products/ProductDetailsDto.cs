using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Products;

public sealed class ProductDetailsDto
{
    public string Version { get; init; } = string.Empty;
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public ProductCategory Category { get; set; }

    public string Image { get; set; } = string.Empty;

    public UnitOfMeasure Unit { get; set; }

    public decimal? UnitAmount { get; set; }

    public UnitOfMeasure? UnitAmountMeasure { get; set; }

    public int StockQuantity { get; set; }

    // Administrative sales setting; stock and live cart reservations are checked separately.
    public bool Available { get; set; }

    public bool InStock { get; set; }

    public bool DeliveryAvailable { get; set; }
}
