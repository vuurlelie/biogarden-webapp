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

    public async Task<IReadOnlyList<ProductDetailsDto>> GetAllDetailsAsync(CancellationToken cancellationToken = default)
    {
        var products = await _unitOfWork.ProductRepository.GetAllAsync(cancellationToken);
        return products.Select(product => product.ToDetailsDto()).ToList();
    }

    public async Task<int> CreateAsync(ProductCreateDto newProduct, CancellationToken cancellationToken = default)
    {
        Normalize(newProduct);
        var entity = newProduct.ToEntity();

        await _unitOfWork.ProductRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    public async Task UpdateAsync(int productId, ProductEditDto changes, CancellationToken cancellationToken = default)
    {
        Normalize(changes);
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

    public async Task<bool> IsImageInUseAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var products = await _unitOfWork.ProductRepository.GetAllAsync(cancellationToken);
        return products.Any(product => string.Equals(product.Image, imagePath, StringComparison.OrdinalIgnoreCase));
    }

    private static void Normalize(ProductCreateDto product)
    {
        product.Name = product.Name.Trim();
        product.Description = NormalizeOptional(product.Description);
        product.Image = NormalizeOptional(product.Image);
        if (product.StockQuantity == 0) product.Available = false;
    }

    private static void Normalize(ProductEditDto product)
    {
        product.Name = product.Name.Trim();
        product.Description = NormalizeOptional(product.Description);
        product.Image = product.Image?.Trim();
        if (product.StockQuantity == 0) product.Available = false;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
