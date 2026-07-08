namespace Biogarden.BusinessLogic.DTOs.Admin;

public sealed class AdminInventoryItemDto
{
    public int ProductId { get; init; }
    public required string Name { get; init; }
    public required string Image { get; init; }
    public UnitOfMeasure Unit { get; init; }
    public int StockQuantity { get; init; }
    public int ReservedQuantity { get; init; }
    public int AvailableQuantity { get; init; }
    public bool Available { get; init; }
    public bool DeliveryAvailable { get; init; }
}

public sealed class UpdateAdminInventoryDto
{
    [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }
    public bool Available { get; set; }
}
