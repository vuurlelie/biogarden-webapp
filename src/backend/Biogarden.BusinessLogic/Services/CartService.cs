using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;

namespace Biogarden.BusinessLogic.Services;

public class CartService : ICartService
{
    private const int CartExpirationInMinutes = 30;

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CartService(IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<CartDto> GetCartAsync(
        Guid? userId,
        string? sessionId,
        CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(
            userId,
            sessionId,
            createIfMissing: true,
            cancellationToken);

        TouchCart(cart, GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapCartAsync(cart, cancellationToken);
    }

    public async Task<CartDto> AddItemAsync(
        Guid? userId,
        string? sessionId,
        AddCartItemDto addCartItemDto,
        CancellationToken cancellationToken = default)
    {
        if (addCartItemDto.Quantity <= 0)
        {
            throw new ArgumentException("A mennyiségnek pozitívnak kell lennie.");
        }

        var cart = await GetOrCreateCartAsync(
            userId,
            sessionId,
            createIfMissing: true,
            cancellationToken);

        var product = await _unitOfWork.ProductRepository.GetByIdAsync(
            addCartItemDto.ProductId,
            cancellationToken);

        if (product is null)
        {
            throw new KeyNotFoundException(
                $"Product with id {addCartItemDto.ProductId} was not found.");
        }

        if (!product.Available)
        {
            throw new InvalidOperationException("A termék jelenleg nem elérhető.");
        }

        var existingCartItem = FindItemByProductId(cart, addCartItemDto.ProductId);

        var targetQuantity =
            existingCartItem is null
                ? addCartItemDto.Quantity
                : existingCartItem.Quantity + addCartItemDto.Quantity;

        await EnsureEnoughStockAsync(
            productId: product.Id,
            requestedQuantity: targetQuantity,
            cartId: cart.Id,
            cancellationToken);

        var now = GetUtcNow();

        if (existingCartItem is null)
        {
            var cartItem = CreateCartItem(
                cart,
                product,
                addCartItemDto.Quantity,
                now);

            await _unitOfWork.CartRepository.AddItemAsync(cartItem, cancellationToken);
        }
        else
        {
            UpdateCartItemQuantity(existingCartItem, targetQuantity, now);
        }

        TouchCart(cart, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedCart = await ReloadCartAsync(cart.Id, cancellationToken);

        return await MapCartAsync(updatedCart, cancellationToken);
    }

    public async Task<int> GetAvailableQuantityToAddAsync(
        Guid? userId,
        string? sessionId,
        int productId,
        CancellationToken cancellationToken = default)
    {
        var product = await _unitOfWork.ProductRepository.GetByIdAsync(productId, cancellationToken);
        if (product is null) throw new KeyNotFoundException($"Product with id {productId} was not found.");
        if (!product.Available) return 0;

        var cart = await ResolveExistingCartAsync(userId, sessionId, cancellationToken);
        var reservedByOtherCarts = await _unitOfWork.CartRepository
            .GetActiveReservedQuantityForProductAsync(productId, cart?.Id, cancellationToken);
        var quantityAlreadyInCart = cart?.Items
            .SingleOrDefault(item => item.ProductId == productId)?.Quantity ?? 0;

        return Math.Max(0, product.StockQuantity - reservedByOtherCarts - quantityAlreadyInCart);
    }

    public async Task<CartDto> UpdateItemQuantityAsync(
        Guid? userId,
        string? sessionId,
        Guid cartItemId,
        UpdateCartItemQuantityDto updateCartItemQuantityDto,
        CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(
            userId,
            sessionId,
            createIfMissing: false,
            cancellationToken);

        var cartItem = cart.Items
            .SingleOrDefault(item => item.Id == cartItemId);

        if (cartItem is null)
        {
            throw new KeyNotFoundException(
                $"Cart item with id {cartItemId} was not found.");
        }

        if (updateCartItemQuantityDto.Quantity <= 0)
        {
            cart.Items.Remove(cartItem);
            TouchCart(cart, GetUtcNow());

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var updatedCartAfterRemove = await ReloadCartAsync(cart.Id, cancellationToken);

            return await MapCartAsync(updatedCartAfterRemove, cancellationToken);
        }

        await EnsureEnoughStockAsync(
            productId: cartItem.ProductId,
            requestedQuantity: updateCartItemQuantityDto.Quantity,
            cartId: cart.Id,
            cancellationToken);

        var now = GetUtcNow();
        cartItem.Quantity = updateCartItemQuantityDto.Quantity;
        cartItem.UpdatedAtUtc = now;

        TouchCart(cart, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedCart = await ReloadCartAsync(cart.Id, cancellationToken);

        return await MapCartAsync(updatedCart, cancellationToken);
    }

    public async Task<CartDto> RemoveItemAsync(
        Guid? userId,
        string? sessionId,
        Guid cartItemId,
        CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(
            userId,
            sessionId,
            createIfMissing: false,
            cancellationToken);

        var cartItem = cart.Items
            .SingleOrDefault(item => item.Id == cartItemId);

        if (cartItem is null)
        {
            throw new KeyNotFoundException(
                $"Cart item with id {cartItemId} was not found.");
        }

        cart.Items.Remove(cartItem);

        TouchCart(cart, GetUtcNow());

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedCart = await ReloadCartAsync(cart.Id, cancellationToken);

        return await MapCartAsync(updatedCart, cancellationToken);
    }

    public async Task<CartDto> ClearCartAsync(
        Guid? userId,
        string? sessionId,
        CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(
            userId,
            sessionId,
            createIfMissing: false,
            cancellationToken);

        cart.Items.Clear();

        TouchCart(cart, GetUtcNow());

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedCart = await ReloadCartAsync(cart.Id, cancellationToken);

        return await MapCartAsync(updatedCart, cancellationToken);
    }

    public async Task<CartDto> MergeGuestCartIntoUserCartAsync(
        Guid userId,
        string guestSessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(guestSessionId))
        {
            throw new ArgumentException("A guest session azonosító nem lehet üres.");
        }

        var userCart = await GetOrCreateCartAsync(
            userId,
            sessionId: null,
            createIfMissing: true,
            cancellationToken);

        var guestCart = await _unitOfWork.CartRepository.GetActiveBySessionIdAsync(
            guestSessionId.Trim(),
            cancellationToken);

        if (guestCart is null || guestCart.Id == userCart.Id)
        {
            return await MapCartAsync(userCart, cancellationToken);
        }

        var now = GetUtcNow();

        foreach (var guestCartItem in guestCart.Items.ToList())
        {
            var existingUserCartItem = FindItemByProductId(userCart, guestCartItem.ProductId);

            var mergedQuantity =
                existingUserCartItem is null
                    ? guestCartItem.Quantity
                    : existingUserCartItem.Quantity + guestCartItem.Quantity;

            await EnsureEnoughStockAsync(
                productId: guestCartItem.ProductId,
                requestedQuantity: mergedQuantity,
                cartId: userCart.Id,
                cancellationToken,
                additionalExcludedCartId: guestCart.Id);

            if (existingUserCartItem is null)
            {
                var product = guestCartItem.Product ??
                    await _unitOfWork.ProductRepository.GetByIdAsync(
                        guestCartItem.ProductId,
                        cancellationToken)
                    ?? throw new KeyNotFoundException(
                        $"Product with id {guestCartItem.ProductId} was not found.");
                var cartItem = CreateCartItem(
                    userCart,
                    product,
                    guestCartItem.Quantity,
                    now);

                await _unitOfWork.CartRepository.AddItemAsync(cartItem, cancellationToken);
            }
            else
            {
                UpdateCartItemQuantity(existingUserCartItem, mergedQuantity, now);
            }
        }

        TouchCart(userCart, now);

        guestCart.IsCheckedOut = true;
        guestCart.CheckedOutAtUtc = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedCart = await ReloadCartAsync(userCart.Id, cancellationToken);

        return await MapCartAsync(updatedCart, cancellationToken);
    }

    public async Task<CartValidationResultDto> ValidateCartForCheckoutAsync(
        Guid? userId,
        string? sessionId,
        CancellationToken cancellationToken = default)
    {
        var cart = await ResolveExistingCartAsync(userId, sessionId, cancellationToken);

        if (cart is null)
        {
            return new CartValidationResultDto
            {
                IsValid = false,
                Issues =
                [
                    new CartValidationIssueDto
                    {
                        Message = "A kosár nem található."
                    }
                ]
            };
        }

        if (cart.Items.Count == 0)
        {
            return new CartValidationResultDto
            {
                IsValid = false,
                Issues =
                [
                    new CartValidationIssueDto
                    {
                        Message = "A kosár üres."
                    }
                ]
            };
        }

        var issues = new List<CartValidationIssueDto>();

        foreach (var cartItem in cart.Items)
        {
            var product = await _unitOfWork.ProductRepository.GetByIdAsync(
                cartItem.ProductId,
                cancellationToken);

            if (product is null)
            {
                issues.Add(new CartValidationIssueDto
                {
                    ProductId = cartItem.ProductId,
                    ProductName = cartItem.Product?.Name ?? string.Empty,
                    Message = "A termék már nem létezik."
                });

                continue;
            }

            if (!product.Available)
            {
                issues.Add(new CartValidationIssueDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Message = "A termék jelenleg nem elérhető."
                });

                continue;
            }

            var reservedQuantity =
                await _unitOfWork.CartRepository.GetActiveReservedQuantityForProductAsync(
                    product.Id,
                    cart.Id,
                    cancellationToken);

            var availableStock = Math.Max(0, product.StockQuantity - reservedQuantity);

            if (cartItem.Quantity > availableStock)
            {
                issues.Add(new CartValidationIssueDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Message =
                        $"A kért mennyiség nem elérhető. Jelenleg legfeljebb {availableStock} db foglalható."
                });
            }
        }

        return new CartValidationResultDto
        {
            IsValid = issues.Count == 0,
            Issues = issues
        };
    }

    public async Task<int> CleanupExpiredCartsAsync(CancellationToken cancellationToken = default)
    {
        var expiredCarts = await _unitOfWork.CartRepository.GetExpiredCartsAsync(cancellationToken);

        if (expiredCarts.Count == 0)
        {
            return 0;
        }

        foreach (var expiredCart in expiredCarts)
        {
            _unitOfWork.CartRepository.Remove(expiredCart);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return expiredCarts.Count;
    }

    private async Task<Cart> GetOrCreateCartAsync(
        Guid? userId,
        string? sessionId,
        bool createIfMissing,
        CancellationToken cancellationToken)
    {
        var existingCart = await ResolveExistingCartAsync(userId, sessionId, cancellationToken);

        if (existingCart is not null)
        {
            return existingCart;
        }

        if (!createIfMissing)
        {
            throw new KeyNotFoundException("A kosár nem található.");
        }

        if (userId is null && string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException(
                "Kosárhoz vagy bejelentkezett felhasználó, vagy session azonosító szükséges.");
        }

        var now = GetUtcNow();
        var cart = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionId = NormalizeOptional(sessionId),
            CreatedAtUtc = now,
            LastActivityAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(CartExpirationInMinutes),
            IsCheckedOut = false
        };

        await _unitOfWork.CartRepository.AddAsync(cart, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ReloadCartAsync(cart.Id, cancellationToken);
    }

    private async Task<Cart?> ResolveExistingCartAsync(
        Guid? userId,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        if (userId.HasValue)
        {
            var userCart = await _unitOfWork.CartRepository.GetActiveByUserIdAsync(
                userId.Value,
                cancellationToken);

            if (userCart is not null)
            {
                return userCart;
            }
        }

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            return await _unitOfWork.CartRepository.GetActiveBySessionIdAsync(
                sessionId.Trim(),
                cancellationToken);
        }

        return null;
    }

    private async Task<Cart> ReloadCartAsync(
        Guid cartId,
        CancellationToken cancellationToken)
    {
        var cart = await _unitOfWork.CartRepository.GetByIdWithItemsAsync(
            cartId,
            cancellationToken);

        if (cart is null)
        {
            throw new KeyNotFoundException($"Cart with id {cartId} was not found.");
        }

        return cart;
    }

    private async Task<CartDto> MapCartAsync(
        Cart cart,
        CancellationToken cancellationToken)
    {
        var reservedQuantitiesByProductId = new Dictionary<int, int>();

        foreach (var cartItem in cart.Items)
        {
            if (reservedQuantitiesByProductId.ContainsKey(cartItem.ProductId))
            {
                continue;
            }

            var reservedQuantity =
                await _unitOfWork.CartRepository.GetActiveReservedQuantityForProductAsync(
                    cartItem.ProductId,
                    cart.Id,
                    cancellationToken);

            reservedQuantitiesByProductId[cartItem.ProductId] = reservedQuantity;
        }

        return cart.ToDto(reservedQuantitiesByProductId);
    }

    private async Task EnsureEnoughStockAsync(
        int productId,
        int requestedQuantity,
        Guid cartId,
        CancellationToken cancellationToken,
        Guid? additionalExcludedCartId = null)
    {
        var product = await _unitOfWork.ProductRepository.GetByIdAsync(
            productId,
            cancellationToken);

        if (product is null)
        {
            throw new KeyNotFoundException(
                $"Product with id {productId} was not found.");
        }

        if (!product.Available)
        {
            throw new InvalidOperationException("A termék jelenleg nem elérhető.");
        }

        // A merge transfers existing reservations; neither participating cart is a competitor.
        var reservedQuantity = additionalExcludedCartId.HasValue
            ? await _unitOfWork.CartRepository.GetActiveReservedQuantityForProductAsync(
                productId, cartId, additionalExcludedCartId.Value, cancellationToken)
            : await _unitOfWork.CartRepository.GetActiveReservedQuantityForProductAsync(
                productId, cartId, cancellationToken);

        var availableStock = Math.Max(0, product.StockQuantity - reservedQuantity);

        if (requestedQuantity > availableStock)
        {
            throw new InvalidOperationException(
                $"A kért mennyiség nem elérhető. Jelenleg legfeljebb {availableStock} db foglalható.");
        }

        _unitOfWork.ProductRepository.GuardStockReservation(product);
    }

    private DateTime GetUtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static CartItem? FindItemByProductId(Cart cart, int productId) =>
        cart.Items.SingleOrDefault(item => item.ProductId == productId);

    private static CartItem CreateCartItem(
        Cart cart,
        Product product,
        int quantity,
        DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            CartId = cart.Id,
            Cart = cart,
            ProductId = product.Id,
            Product = product,
            Quantity = quantity,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private static void UpdateCartItemQuantity(CartItem cartItem, int quantity, DateTime now)
    {
        cartItem.Quantity = quantity;
        cartItem.UpdatedAtUtc = now;
    }

    private static void TouchCart(Cart cart, DateTime now)
    {
        cart.LastActivityAtUtc = now;
        cart.ExpiresAtUtc = now.AddMinutes(CartExpirationInMinutes);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
