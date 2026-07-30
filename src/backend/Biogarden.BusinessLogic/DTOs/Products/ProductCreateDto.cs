using System.ComponentModel.DataAnnotations;
using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Products;

public sealed class ProductCreateDto
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, 10000000, ErrorMessage = "Az ár 0 és 10 000 000 Ft között lehet.")]
    public decimal Price { get; set; }

    public ProductCategory Category { get; set; }

    [StringLength(500)]
    public string? Image { get; set; }

    public UnitOfMeasure Unit { get; set; }

    [Range(0.01, 10000000, ErrorMessage = "A kiszerelés 0,01 és 10 000 000 között lehet.")]
    public decimal? UnitAmount { get; set; }

    public UnitOfMeasure? UnitAmountMeasure { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "A készlet nem lehet negatív.")]
    public int StockQuantity { get; set; }

    public bool Available { get; set; }

    public bool DeliveryAvailable { get; set; }
}
