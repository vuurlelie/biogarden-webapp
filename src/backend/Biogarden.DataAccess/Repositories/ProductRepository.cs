using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
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
            .SingleOrDefaultAsync(
                product =>
                    product.Id == productId &&
                    product.RecordStatus == RecordStatus.Active,
                cancellationToken);
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

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _databaseContext.Products.AddAsync(product, cancellationToken);
    }

    public void Update(Product product)
    {
        _databaseContext.Products.Update(product);
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
