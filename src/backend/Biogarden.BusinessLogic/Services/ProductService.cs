using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Products;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.DataAccess.Abstractions;

namespace Biogarden.BusinessLogic.Services;

public sealed class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ProductListItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await _unitOfWork.ProductRepository.GetAllAsync(cancellationToken);
        var items = products.ToListItemDtos();

        return items;
    }

    public async Task<ProductDetailsDto?> GetDetailsAsync(int productId, CancellationToken cancellationToken = default)
    {
        var product = await _unitOfWork.ProductRepository.GetByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var details = product.ToDetailsDto();

        return details;
    }

    public async Task<int> CreateAsync(ProductCreateDto newProduct, CancellationToken cancellationToken = default)
    {
        var entity = newProduct.ToEntity();

        await _unitOfWork.ProductRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    public async Task UpdateAsync(int productId, ProductEditDto changes, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.ProductRepository.GetByIdAsync(productId, cancellationToken);
        if (entity is null)
        {
            throw new KeyNotFoundException($"Product {productId} not found.");
        }

        changes.ApplyChanges(entity);

        _unitOfWork.ProductRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(int productId, CancellationToken cancellationToken = default)
    {
        var deleted = await _unitOfWork.ProductRepository.DeleteByIdAsync(productId, cancellationToken);
        if (deleted)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return deleted;
    }
}