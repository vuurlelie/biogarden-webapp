using Biogarden.DataAccess.Enums;

public class Product
{
    public int Id { get; set; }

    public required string Name { get; set; }

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

    public RecordStatus RecordStatus { get; set; } = RecordStatus.Active;
}