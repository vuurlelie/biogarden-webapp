using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.BusinessLogic.Services;

public sealed class AdminInventoryService : IAdminInventoryService
{
    private readonly IAppDbContext _databaseContext;
    private readonly IUnitOfWork _unitOfWork;

    public AdminInventoryService(IAppDbContext databaseContext, IUnitOfWork unitOfWork)
    {
        _databaseContext = databaseContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResultDto<AdminInventoryItemDto>> GetPageAsync(AdminInventoryListQuery filter, CancellationToken cancellationToken = default)
    {
        var query = InventoryQuery();
        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) query = query.Where(x => x.Name.Contains(search));
        query = filter.Stock switch
        {
            "low" => query.Where(x => x.AvailableQuantity > 0 && x.AvailableQuantity <= 5),
            "empty" => query.Where(x => x.AvailableQuantity == 0),
            "reserved" => query.Where(x => x.ReservedQuantity > 0),
            _ => query
        };
        var count = await query.CountAsync(cancellationToken);
        var page = AdminPaging.Page(filter, count);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.ProductId)
            .Skip((page - 1) * filter.NormalizedPageSize).Take(filter.NormalizedPageSize).ToListAsync(cancellationToken);
        return new(items, page, filter.NormalizedPageSize, count);
    }

    public Task<AdminInventoryItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => InventoryQuery().SingleOrDefaultAsync(x => x.ProductId == id, cancellationToken);

    private IQueryable<AdminInventoryItemDto> InventoryQuery()
    {
        var now = DateTime.UtcNow;
        return _databaseContext.Products.AsNoTracking()
            .Where(product => product.RecordStatus == RecordStatus.Active)
            .OrderBy(product => product.Name)
            .Select(product => new AdminInventoryItemDto
            {
                ProductId = product.Id,
                Version = Convert.ToBase64String(product.RowVersion),
                Name = product.Name,
                Image = product.Image ?? Biogarden.BusinessLogic.BusinessRules.DefaultProductImagePath,
                Unit = product.Unit,
                StockQuantity = product.StockQuantity,
                ReservedQuantity = _databaseContext.CartItems
                    .Where(item => item.ProductId == product.Id && item.Cart != null && !item.Cart.IsCheckedOut && item.Cart.ExpiresAtUtc > now)
                    .Sum(item => (int?)item.Quantity) ?? 0,
                AvailableQuantity = product.StockQuantity - (_databaseContext.CartItems
                    .Where(item => item.ProductId == product.Id && item.Cart != null && !item.Cart.IsCheckedOut && item.Cart.ExpiresAtUtc > now)
                    .Sum(item => (int?)item.Quantity) ?? 0) > 0
                    ? product.StockQuantity - (_databaseContext.CartItems
                        .Where(item => item.ProductId == product.Id && item.Cart != null && !item.Cart.IsCheckedOut && item.Cart.ExpiresAtUtc > now)
                        .Sum(item => (int?)item.Quantity) ?? 0)
                    : 0,
                Available = product.Available,
                DeliveryAvailable = product.DeliveryAvailable
            });
    }

    public async Task<AdminInventoryItemDto> UpdateAsync(int productId, UpdateAdminInventoryDto changes, CancellationToken cancellationToken = default)
    {
        var product = await _unitOfWork.ProductRepository.GetByIdAsync(productId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product {productId} not found.");
        AdminEditVersion.RequireCurrent(changes.Version, product.RowVersion);
        var reserved = await StockUpdatePolicy.ValidateAsync(_unitOfWork, product, changes.StockQuantity, cancellationToken);

        product.StockQuantity = changes.StockQuantity;
        product.Available = changes.StockQuantity > 0 && changes.Available;
        _unitOfWork.ProductRepository.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AdminInventoryItemDto
        {
            ProductId = product.Id,
            Version = Convert.ToBase64String(product.RowVersion),
            Name = product.Name,
            Image = product.Image ?? Biogarden.BusinessLogic.BusinessRules.DefaultProductImagePath,
            Unit = product.Unit,
            StockQuantity = product.StockQuantity,
            ReservedQuantity = reserved,
            AvailableQuantity = Math.Max(0, product.StockQuantity - reserved),
            Available = product.Available,
            DeliveryAvailable = product.DeliveryAvailable
        };
    }
}
