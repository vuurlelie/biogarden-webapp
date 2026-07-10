using System.ComponentModel.DataAnnotations;
using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Products;

public sealed class ProductCreateDto
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, 10000000)]
    public decimal Price { get; set; }

    public ProductCategory Category { get; set; }

    [StringLength(500)]
    public string? Image { get; set; }

    public UnitOfMeasure Unit { get; set; }

    [Range(typeof(decimal), "0.01", "10000000")]
    public decimal? UnitAmount { get; set; }

    public UnitOfMeasure? UnitAmountMeasure { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    public bool Available { get; set; }

    public bool DeliveryAvailable { get; set; }
}
