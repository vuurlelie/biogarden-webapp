using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Biogarden.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly IAppDbContext _databaseContext;

    public ProductRepository(IAppDbContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        return await _databaseContext.Products
            .SingleOrDefaultAsync(product => product.Id == productId && product.RecordStatus == RecordStatus.Active, cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await _databaseContext.Products
            .AsNoTracking()
            .Where(product => product.RecordStatus == RecordStatus.Active)
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);

        return products;
    }

    public async Task<PagedResult<Product>> GetPagedAsync(
        string? search,
        IReadOnlyCollection<ProductCategory> categories,
        bool availableOnly,
        bool unavailableOnly,
        bool deliveryOnly,
        string sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var query = _databaseContext.Products
            .AsNoTracking()
            .Where(product => product.RecordStatus == RecordStatus.Active);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(product => product.Name.Contains(normalizedSearch));
        }

        if (categories.Count > 0)
        {
            query = query.Where(product => categories.Contains(product.Category));
        }

        if (availableOnly)
        {
            query = query.Where(product => product.Available && product.StockQuantity > 0);
        }

        if (unavailableOnly)
        {
            query = query.Where(product => !product.Available || product.StockQuantity <= 0);
        }

        if (deliveryOnly)
        {
            query = query.Where(product => product.DeliveryAvailable);
        }

        query = sort switch
        {
            "price-asc" => query.OrderBy(product => product.Price).ThenBy(product => product.Name),
            "price-desc" => query.OrderByDescending(product => product.Price).ThenBy(product => product.Name),
            _ => query.OrderBy(product => product.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;
        if (offset >= totalCount)
        {
            return new PagedResult<Product>([], page, pageSize, totalCount);
        }

        var items = await query
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Product>(items, page, pageSize, totalCount);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _databaseContext.Products.AddAsync(product, cancellationToken);
    }

    public void Update(Product product)
    {
        _databaseContext.Products.Update(product);
    }

    public void GuardStockReservation(Product product)
    {
        // An unchanged stock write checks and advances RowVersion in the same SaveChanges
        // transaction as the cart reservation, preventing concurrent over-reservation.
        _databaseContext.Products.Entry(product).Property(item => item.StockQuantity).IsModified = true;
    }

    public async Task<bool> DeleteByIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        var product = await _databaseContext.Products
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);

        if (product is null || product.RecordStatus == RecordStatus.Deleted)
        {
            return false;
        }

        product.RecordStatus = RecordStatus.Deleted;
        _databaseContext.Products.Update(product);

        return true;
    }
}
