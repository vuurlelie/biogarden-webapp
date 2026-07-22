using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.BusinessLogic.Services;

public sealed class AdminDashboardService : IAdminDashboardService
{
    private const int LowStockThreshold = 5;
    private const int PreviewItemCount = 5;

    private readonly IAppDbContext _databaseContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminDashboardService(
        IAppDbContext databaseContext,
        UserManager<ApplicationUser> userManager)
    {
        _databaseContext = databaseContext;
        _userManager = userManager;
    }

    public async Task<AdminDashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var todayUtc = DateTime.UtcNow.Date;

        var activeProducts = _databaseContext.Products
            .Where(product => product.RecordStatus == RecordStatus.Active);

        var productCount = await activeProducts.CountAsync(cancellationToken);
        var availableProductCount = await activeProducts
            .CountAsync(product => product.Available && product.StockQuantity > 0, cancellationToken);
        var lowStockProductCount = await activeProducts
            .CountAsync(product => product.StockQuantity <= LowStockThreshold, cancellationToken);
        var pendingOrderCount = await _databaseContext.Orders
            .CountAsync(order => order.Status == OrderStatus.Pending, cancellationToken);
        var todayOrderCount = await _databaseContext.Orders
            .CountAsync(order => order.CreatedAtUtc >= todayUtc, cancellationToken);
        var publishedPostCount = await _databaseContext.Posts
            .CountAsync(post => !post.Draft, cancellationToken);
        var activeOrderValue = await _databaseContext.Orders
            .Where(order => order.Status != OrderStatus.Cancelled)
            .SumAsync(order => (decimal?)order.TotalPrice, cancellationToken);
        var userCount = await _userManager.Users.CountAsync(cancellationToken);

        var recentOrders = await _databaseContext.Orders
            .AsNoTracking()
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(PreviewItemCount)
            .Select(order => new AdminRecentOrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerName = order.CustomerName,
                CreatedAtUtc = order.CreatedAtUtc,
                TotalPrice = order.TotalPrice,
                ItemCount = order.Items.Sum(item => item.Quantity),
                Status = order.Status,
                FulfillmentMethod = order.FulfillmentMethod
            })
            .ToListAsync(cancellationToken);

        var lowStockProducts = await activeProducts
            .AsNoTracking()
            .Where(product => product.StockQuantity <= LowStockThreshold)
            .OrderBy(product => product.StockQuantity)
            .ThenBy(product => product.Name)
            .Take(PreviewItemCount)
            .Select(product => new AdminLowStockProductDto
            {
                Id = product.Id,
                Name = product.Name,
                StockQuantity = product.StockQuantity,
                Available = product.Available
            })
            .ToListAsync(cancellationToken);

        return new AdminDashboardDto
        {
            ProductCount = productCount,
            AvailableProductCount = availableProductCount,
            LowStockProductCount = lowStockProductCount,
            PendingOrderCount = pendingOrderCount,
            TodayOrderCount = todayOrderCount,
            UserCount = userCount,
            PublishedPostCount = publishedPostCount,
            ActiveOrderValue = activeOrderValue ?? 0,
            RecentOrders = recentOrders,
            LowStockProducts = lowStockProducts
        };
    }
}
