using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.DataAccess.Entities;

namespace Biogarden.BusinessLogic.Mappers;

public static class CartMapper
{
    public static CartDto ToDto(
        this Cart cart,
        IReadOnlyDictionary<int, int> reservedQuantitiesByProductId)
    {
        var itemDtos = cart.Items
            .OrderBy(cartItem => cartItem.CreatedAtUtc)
            .Select(cartItem =>
            {
                var reservedQuantityExcludingCurrentCart =
                    reservedQuantitiesByProductId.TryGetValue(
                        cartItem.ProductId,
                        out var reservedQuantity)
                        ? reservedQuantity
                        : 0;

                var availableQuantityForCart =
                    Math.Max(
                        0,
                        cartItem.Product.StockQuantity - reservedQuantityExcludingCurrentCart);

                return new CartItemDto
                {
                    Id = cartItem.Id,
                    ProductId = cartItem.ProductId,
                    ProductName = cartItem.Product.Name,
                    ProductDescription = cartItem.Product.Description,
                    UnitPrice = cartItem.Product.Price,
                    Image = cartItem.Product.Image,
                    Unit = cartItem.Product.Unit,
                    Quantity = cartItem.Quantity,
                    StockQuantity = cartItem.Product.StockQuantity,
                    ReservedQuantityExcludingCurrentCart = reservedQuantityExcludingCurrentCart,
                    AvailableQuantityForCart = availableQuantityForCart,
                    Available = cartItem.Product.Available,
                    DeliveryAvailable = cartItem.Product.DeliveryAvailable,
                    LineTotal = cartItem.Product.Price * cartItem.Quantity
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
