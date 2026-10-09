using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.DataAccess.Entities;

namespace Biogarden.BusinessLogic.Mappers;

public static class CartMapper
{
    public static CartDto ToDto(this Cart cart, IReadOnlyDictionary<int, int> reservedQuantitiesByProductId)
    {
        var itemDtos = cart.Items
            .OrderBy(cartItem => cartItem.CreatedAtUtc)
            .Select(cartItem =>
            {
                var product = cartItem.Product
                    ?? throw new InvalidOperationException("Cart item product must be loaded before mapping.");
                var reservedQuantityExcludingCurrentCart =
                    reservedQuantitiesByProductId.TryGetValue(cartItem.ProductId, out var reservedQuantity)
                        ? reservedQuantity
                        : 0;

                var availableQuantityForCart =
                    Math.Max(0, product.StockQuantity - reservedQuantityExcludingCurrentCart);

                return new CartItemDto
                {
                    Id = cartItem.Id,
                    ProductId = cartItem.ProductId,
                    ProductName = product.Name,
                    ProductDescription = product.Description,
                    UnitPrice = product.Price,
                    Image = product.Image,
                    Unit = product.Unit,
                    Quantity = cartItem.Quantity,
                    StockQuantity = product.StockQuantity,
                    ReservedQuantityExcludingCurrentCart = reservedQuantityExcludingCurrentCart,
                    AvailableQuantityForCart = availableQuantityForCart,
                    Available = product.Available,
                    DeliveryAvailable = product.DeliveryAvailable,
                    LineTotal = product.Price * cartItem.Quantity
                };
            })
            .ToList();

        return new CartDto
        {
            Id = cart.Id,
            UserId = cart.UserId,
            SessionId = cart.SessionId,
            CreatedAtUtc = cart.CreatedAtUtc,
            LastActivityAtUtc = cart.LastActivityAtUtc,
            ExpiresAtUtc = cart.ExpiresAtUtc,
            IsCheckedOut = cart.IsCheckedOut,
            TotalItemCount = itemDtos.Sum(item => item.Quantity),
            TotalPrice = itemDtos.Sum(item => item.LineTotal),
            Items = itemDtos
        };
    }
}
