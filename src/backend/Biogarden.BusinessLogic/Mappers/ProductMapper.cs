using Biogarden.BusinessLogic.DTOs.Products;
using Biogarden.DataAccess.Entities;

namespace Biogarden.BusinessLogic.Mappers;

public static class ProductMapper
{
    public static ProductListItemDto ToListItemDto(this Product product)
        => MapListItem(product);

    public static IReadOnlyList<ProductListItemDto> ToListItemDtos(this IEnumerable<Product> products)
        => products
            .Select(MapListItem)
            .ToList();

    public static ProductDetailsDto ToDetailsDto(this Product product)
        => new ProductDetailsDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Category = product.Category,
            Image = product.Image ?? BusinessRules.DefaultProductImagePath,
            Unit = product.Unit,
            UnitAmount = product.UnitAmount,
            UnitAmountMeasure = product.UnitAmountMeasure,
            StockQuantity = product.StockQuantity,
            Available = product.Available,
            DeliveryAvailable = product.DeliveryAvailable
        };

    public static Product ToEntity(this ProductCreateDto dto)
    {
        return new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            Category = dto.Category,
            Image = string.IsNullOrWhiteSpace(dto.Image) ? BusinessRules.DefaultProductImagePath : dto.Image,
            Unit = dto.Unit,
            UnitAmount = dto.UnitAmount,
            UnitAmountMeasure = dto.UnitAmountMeasure,
            StockQuantity = dto.StockQuantity,
            Available = dto.Available,
            DeliveryAvailable = dto.DeliveryAvailable
        };
    }

    public static void ApplyChanges(this ProductEditDto changes, Product entity)
    {
        entity.Name = changes.Name;
        entity.Description = changes.Description;
        entity.Price = changes.Price;
        entity.Category = changes.Category;

        if (changes.Image is not null)
        {
            entity.Image = string.IsNullOrWhiteSpace(changes.Image)
                ? BusinessRules.DefaultProductImagePath
                : changes.Image;
        }

        entity.Unit = changes.Unit;
        entity.UnitAmount = changes.UnitAmount;
        entity.UnitAmountMeasure = changes.UnitAmountMeasure;
        entity.StockQuantity = changes.StockQuantity;
        entity.Available = changes.Available;
        entity.DeliveryAvailable = changes.DeliveryAvailable;
    }

    private static ProductListItemDto MapListItem(Product product)
        => new ProductListItemDto
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price,
            Category = product.Category,
            Image = product.Image ?? BusinessRules.DefaultProductImagePath,
            Unit = product.Unit,
            UnitAmount = product.UnitAmount,
            UnitAmountMeasure = product.UnitAmountMeasure,
            Available = product.Available,
            DeliveryAvailable = product.DeliveryAvailable
        };
}
