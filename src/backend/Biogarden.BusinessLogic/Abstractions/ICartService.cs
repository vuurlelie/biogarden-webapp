using Biogarden.BusinessLogic.DTOs.Carts;

namespace Biogarden.BusinessLogic.Abstractions;

public interface ICartService
{
    Task<CartDto> GetCartAsync(
        Guid? userId,
        string? sessionId,
        CancellationToken cancellationToken = default);

    Task<CartDto> AddItemAsync(
        Guid? userId,
        string? sessionId,
        AddCartItemDto addCartItemDto,
        CancellationToken cancellationToken = default);

    Task<CartDto> UpdateItemQuantityAsync(
        Guid? userId,
        string? sessionId,
        Guid cartItemId,
        UpdateCartItemQuantityDto updateCartItemQuantityDto,
        CancellationToken cancellationToken = default);

    Task<CartDto> RemoveItemAsync(
        Guid? userId,
        string? sessionId,
        Guid cartItemId,
        CancellationToken cancellationToken = default);

    Task<CartDto> ClearCartAsync(
        Guid? userId,
        string? sessionId,
        CancellationToken cancellationToken = default);

    Task<CartDto> MergeGuestCartIntoUserCartAsync(
        Guid userId,
        string guestSessionId,
        CancellationToken cancellationToken = default);

    Task<CartValidationResultDto> ValidateCartForCheckoutAsync(
        Guid? userId,
        string? sessionId,
        CancellationToken cancellationToken = default);

    Task<int> CleanupExpiredCartsAsync(CancellationToken cancellationToken = default);
}