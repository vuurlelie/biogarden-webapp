using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.DataAccess.Abstractions;
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

    public async Task<IReadOnlyList<AdminInventoryItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _databaseContext.Products.AsNoTracking()
            .OrderBy(product => product.Name)
            .Select(product => new AdminInventoryItemDto
            {
                ProductId = product.Id,
                Name = product.Name,
                Image = product.Image ?? Biogarden.BusinessLogic.BusinessRules.DefaultProductImagePath,
                Unit = product.Unit,
                StockQuantity = product.StockQuantity,
                ReservedQuantity = _databaseContext.CartItems
                    .Where(item => item.ProductId == product.Id && !item.Cart.IsCheckedOut && item.Cart.ExpiresAtUtc > now)
                    .Sum(item => (int?)item.Quantity) ?? 0,
                AvailableQuantity = product.StockQuantity - (_databaseContext.CartItems
                    .Where(item => item.ProductId == product.Id && !item.Cart.IsCheckedOut && item.Cart.ExpiresAtUtc > now)
                    .Sum(item => (int?)item.Quantity) ?? 0) > 0
                    ? product.StockQuantity - (_databaseContext.CartItems
                        .Where(item => item.ProductId == product.Id && !item.Cart.IsCheckedOut && item.Cart.ExpiresAtUtc > now)
                        .Sum(item => (int?)item.Quantity) ?? 0)
                    : 0,
                Available = product.Available,
                DeliveryAvailable = product.DeliveryAvailable
            }).ToListAsync(cancellationToken);
    }

    public async Task<AdminInventoryItemDto> UpdateAsync(int productId, UpdateAdminInventoryDto changes, CancellationToken cancellationToken = default)
    {
        var product = await _unitOfWork.ProductRepository.GetByIdAsync(productId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product {productId} not found.");
        var now = DateTime.UtcNow;
        var reserved = await _databaseContext.CartItems
            .Where(item => item.ProductId == productId && !item.Cart.IsCheckedOut && item.Cart.ExpiresAtUtc > now)
            .SumAsync(item => (int?)item.Quantity, cancellationToken) ?? 0;

        if (changes.StockQuantity < reserved)
            throw new InvalidOperationException($"A készlet nem lehet kevesebb a jelenleg lefoglalt {reserved} darabnál.");

        product.StockQuantity = changes.StockQuantity;
        product.Available = changes.StockQuantity > 0 && changes.Available;
        _unitOfWork.ProductRepository.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AdminInventoryItemDto
        {
            ProductId = product.Id, Name = product.Name,
            Image = product.Image ?? Biogarden.BusinessLogic.BusinessRules.DefaultProductImagePath,
            Unit = product.Unit, StockQuantity = product.StockQuantity, ReservedQuantity = reserved,
            AvailableQuantity = Math.Max(0, product.StockQuantity - reserved), Available = product.Available,
            DeliveryAvailable = product.DeliveryAvailable
        };
    }
}
