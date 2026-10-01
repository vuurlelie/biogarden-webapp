namespace Biogarden.BusinessLogic.DTOs.Admin;

public sealed class AdminInventoryItemDto
{
    public string Version { get; init; } = string.Empty;
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
    public string? Version { get; set; }
    public int StockQuantity { get; set; }
    public bool Available { get; set; }
}
