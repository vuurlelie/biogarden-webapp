using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Products;

public sealed class ProductCreateDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public ProductCategory Category { get; set; }

    public string? Image { get; set; }

    public UnitOfMeasure Unit { get; set; }

    public decimal? UnitAmount { get; set; }

    public UnitOfMeasure? UnitAmountMeasure { get; set; }

    public int StockQuantity { get; set; }

    public bool Available { get; set; }

    public bool DeliveryAvailable { get; set; }
}