using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.BusinessLogic.DTOs.Products;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Enums;

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

    public async Task<PagedResultDto<ProductListItemDto>> GetPagedAsync(
        ProductListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var products = await GetPagedProductsAsync(query, cancellationToken);

        return new PagedResultDto<ProductListItemDto>(
            products.Items.ToListItemDtos(),
            products.Page,
            products.PageSize,
            products.TotalCount);
    }

    public async Task<PagedResultDto<ProductDetailsDto>> GetPagedDetailsAsync(
        ProductListQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var products = await GetPagedProductsAsync(query, cancellationToken);

        return new PagedResultDto<ProductDetailsDto>(
            products.Items.Select(product => product.ToDetailsDto()).ToList(),
            products.Page,
            products.PageSize,
            products.TotalCount);
    }

    private async Task<Biogarden.DataAccess.Models.PagedResult<Product>> GetPagedProductsAsync(
        ProductListQueryDto query,
        CancellationToken cancellationToken)
    {
        var page = query.NormalizedPage;
        var pageSize = query.NormalizedPageSize;
        return await _unitOfWork.ProductRepository.GetPagedAsync(
            query.Search,
            GetCategories(query.Category),
            query.AvailableOnly,
            query.UnavailableOnly,
            query.DeliveryOnly,
            query.Sort ?? string.Empty,
            page,
            pageSize,
            cancellationToken);
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

        AdminEditVersion.RequireCurrent(changes.Version, entity.RowVersion);
        await StockUpdatePolicy.ValidateAsync(_unitOfWork, entity, changes.StockQuantity, cancellationToken);
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
    }

    private static void Normalize(ProductEditDto product)
    {
        product.Name = product.Name.Trim();
        product.Description = NormalizeOptional(product.Description);
        product.Image = product.Image?.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProductCategory[] GetCategories(string? category)
    {
        if (int.TryParse(category, out var numericCategory) &&
            Enum.IsDefined(typeof(ProductCategory), numericCategory))
        {
            return [(ProductCategory)numericCategory];
        }

        return category switch
        {
            "zoldsegek" => [ProductCategory.Vegetable],
            "fuszerek-gyogyteak" => [ProductCategory.HerbalTea, ProductCategory.Spice],
            "illoolajok-hidrolatumok" => [ProductCategory.EssentialOil, ProductCategory.Hydrolate],
            _ => []
        };
    }
}
