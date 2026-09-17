using Biogarden.DataAccess.Entities;

using Biogarden.DataAccess.Enums;
using Biogarden.DataAccess.Models;

namespace Biogarden.DataAccess.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<Product>> GetPagedAsync(
        string? search,
        IReadOnlyCollection<ProductCategory> categories,
        bool availableOnly,
        bool unavailableOnly,
        bool deliveryOnly,
        string sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    void Update(Product product);

    void GuardStockReservation(Product product);

    Task<bool> DeleteByIdAsync(int productId, CancellationToken cancellationToken = default);
}
