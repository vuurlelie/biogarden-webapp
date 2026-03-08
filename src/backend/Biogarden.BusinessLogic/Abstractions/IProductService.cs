using Biogarden.BusinessLogic.DTOs.Products;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IProductService
{
    Task<IReadOnlyList<ProductListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ProductDetailsDto?> GetDetailsAsync(int productId, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(ProductCreateDto newProduct, CancellationToken cancellationToken = default);

    Task UpdateAsync(int productId, ProductEditDto changes, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int productId, CancellationToken cancellationToken = default);
}