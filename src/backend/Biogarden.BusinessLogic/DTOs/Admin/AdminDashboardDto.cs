using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Admin;

public sealed class AdminDashboardDto
{
    public int ProductCount { get; init; }
    public int AvailableProductCount { get; init; }
    public int LowStockProductCount { get; init; }
    public int PendingOrderCount { get; init; }
    public int PendingWithdrawalRequestCount { get; init; }
    public int TodayOrderCount { get; init; }
    public int UserCount { get; init; }
    public int PublishedPostCount { get; init; }
    public decimal ActiveOrderValue { get; init; }
    public IReadOnlyList<AdminRecentOrderDto> RecentOrders { get; init; } = [];
    public IReadOnlyList<AdminWithdrawalRequestPreviewDto> PendingWithdrawalRequests { get; init; } = [];
    public IReadOnlyList<AdminLowStockProductDto> LowStockProducts { get; init; } = [];
}

public sealed class AdminRecentOrderDto
{
    public Guid Id { get; init; }
    public required string OrderNumber { get; init; }
    public required string CustomerName { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public decimal TotalPrice { get; init; }
    public int ItemCount { get; init; }
    public OrderStatus Status { get; init; }
    public FulfillmentMethod FulfillmentMethod { get; init; }
}

public sealed class AdminLowStockProductDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public int StockQuantity { get; init; }
    public bool Available { get; init; }
}

public sealed class AdminWithdrawalRequestPreviewDto
{
    public int Id { get; init; }
    public Guid OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required string CustomerName { get; init; }
    public DateTime RequestedAtUtc { get; init; }
}
