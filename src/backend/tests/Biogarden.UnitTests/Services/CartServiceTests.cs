using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class CartServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);
    private readonly Mock<ICartRepository> _carts = new(MockBehavior.Strict);
    private readonly Mock<IProductRepository> _products = new(MockBehavior.Strict);
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 7, 24, 8, 0, 0, TimeSpan.Zero));
    private readonly CartService _service;

    public CartServiceTests()
    {
        _products.Setup(repository => repository.GuardStockReservation(It.IsAny<Product>()));
        _unitOfWork.SetupGet(x => x.CartRepository).Returns(_carts.Object);
        _unitOfWork.SetupGet(x => x.ProductRepository).Returns(_products.Object);
        _service = new CartService(_unitOfWork.Object, _timeProvider);
    }

    [Fact]
    public async Task AddItemAsync_WithNonPositiveQuantity_ThrowsBeforeRepositoryAccess()
    {
        var dto = new AddCartItemDto { ProductId = 1, Quantity = 0 };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddItemAsync(Guid.NewGuid(), null, dto));

        _unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetCartAsync_ExtendsReservationByThirtyMinutes()
    {
        var userId = Guid.NewGuid();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var previousExpiry = now.AddMinutes(2);
        var cart = new Cart { Id = Guid.NewGuid(), UserId = userId, ExpiresAtUtc = previousExpiry };
        _carts.Setup(x => x.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.GetCartAsync(userId, null);

        Assert.True(result.ExpiresAtUtc > previousExpiry);
        Assert.Equal(now, cart.LastActivityAtUtc);
        Assert.Equal(now.AddMinutes(30), result.ExpiresAtUtc);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAvailableQuantityToAddAsync_SubtractsOtherReservationsAndCurrentCartQuantity()
    {
        var userId = Guid.NewGuid();
        var product = new Product { Id = 8, Name = "Uborka", Available = true, StockQuantity = 12 };
        var cart = CreateCart(userId, product.Id, quantity: 3);
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _carts.Setup(x => x.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _carts.Setup(x => x.GetActiveReservedQuantityForProductAsync(product.Id, cart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(4);

        var available = await _service.GetAvailableQuantityToAddAsync(userId, null, product.Id);

        Assert.Equal(5, available);
    }

    [Fact]
    public async Task AddItemAsync_WithNewItem_SavesTrackedCartWithoutUpdatingGraph()
    {
        var userId = Guid.NewGuid();
        var cart = new Cart { Id = Guid.NewGuid(), UserId = userId };
        var product = new Product
        {
            Id = 7,
            Name = "Rozmaring",
            Available = true,
            StockQuantity = 5
        };

        _carts
            .Setup(x => x.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _products
            .Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _carts
            .Setup(x => x.GetActiveReservedQuantityForProductAsync(
                product.Id, cart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _carts
            .Setup(x => x.AddItemAsync(It.IsAny<CartItem>(), It.IsAny<CancellationToken>()))
            .Callback<CartItem, CancellationToken>((item, _) => cart.Items.Add(item))
            .Returns(Task.CompletedTask);
        _carts
            .Setup(x => x.GetByIdWithItemsAsync(cart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                cart.Items.Single().Product = product;
                return cart;
            });

        var result = await _service.AddItemAsync(
            userId,
            sessionId: null,
            new AddCartItemDto { ProductId = product.Id, Quantity = 1 });

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var createdItem = Assert.Single(cart.Items);
        Assert.Equal(1, result.TotalItemCount);
        Assert.Equal(product.Id, Assert.Single(result.Items).ProductId);
        Assert.Equal(now, cart.LastActivityAtUtc);
        Assert.Equal(now.AddMinutes(30), cart.ExpiresAtUtc);
        Assert.Equal(now, createdItem.CreatedAtUtc);
        Assert.Equal(now, createdItem.UpdatedAtUtc);
        _carts.Verify(x => x.Update(It.IsAny<Cart>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateCartForCheckout_WhenCartIsMissing_ReturnsInvalidResult()
    {
        var userId = Guid.NewGuid();
        _carts
            .Setup(x => x.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);

        var result = await _service.ValidateCartForCheckoutAsync(userId, null);

        Assert.False(result.IsValid);
        Assert.Single(result.Issues);
        Assert.Equal("A kosár nem található.", result.Issues[0].Message);
    }

    [Fact]
    public async Task ValidateCartForCheckout_WhenRequestedQuantityExceedsStock_ReturnsIssue()
    {
        var userId = Guid.NewGuid();
        var cart = CreateCart(userId, productId: 3, quantity: 5);
        var product = new Product
        {
            Id = 3,
            Name = "Bazsalikom",
            Available = true,
            StockQuantity = 4
        };

        _carts
            .Setup(x => x.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _products
            .Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _carts
            .Setup(x => x.GetActiveReservedQuantityForProductAsync(
                product.Id, cart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.ValidateCartForCheckoutAsync(userId, null);

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(product.Id, issue.ProductId);
        Assert.Contains("3 db", issue.Message);
    }

    [Fact]
    public async Task MergeGuestCartIntoUserCartAsync_CombinesExistingItemsAndChecksOutGuestCart()
    {
        var userId = Guid.NewGuid();
        const string guestSessionId = " guest-session ";
        var product = new Product
        {
            Id = 3,
            Name = "Bazsalikom",
            Price = 900,
            Available = true,
            StockQuantity = 8
        };
        var userCart = CreateCart(userId, product.Id, quantity: 2);
        userCart.Items.Single().Product = product;
        var guestCart = new Cart
        {
            Id = Guid.NewGuid(),
            SessionId = guestSessionId.Trim()
        };
        guestCart.Items.Add(new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = guestCart.Id,
            ProductId = product.Id,
            Product = product,
            Quantity = 3
        });

        _carts
            .Setup(x => x.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCart);
        _carts
            .Setup(x => x.GetActiveBySessionIdAsync(guestSessionId.Trim(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(guestCart);
        _products
            .Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _carts
            .Setup(x => x.GetActiveReservedQuantityForProductAsync(
                product.Id,
                userCart.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _carts
            .Setup(x => x.GetByIdWithItemsAsync(userCart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCart);

        var result = await _service.MergeGuestCartIntoUserCartAsync(userId, guestSessionId);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        Assert.True(guestCart.IsCheckedOut);
        Assert.Equal(now, guestCart.CheckedOutAtUtc);
        Assert.Equal(now, userCart.LastActivityAtUtc);
        Assert.Equal(now.AddMinutes(30), userCart.ExpiresAtUtc);
        Assert.Equal(5, userCart.Items.Single().Quantity);
        Assert.Equal(5, result.TotalItemCount);
        Assert.Equal(4500, result.TotalPrice);
        _carts.Verify(x => x.AddItemAsync(It.IsAny<CartItem>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MergeGuestCartIntoUserCartAsync_WhenMergedQuantityExceedsStock_DoesNotCheckoutGuestCart()
    {
        var userId = Guid.NewGuid();
        const string guestSessionId = "guest-session";
        var product = new Product
        {
            Id = 3,
            Name = "Bazsalikom",
            Price = 900,
            Available = true,
            StockQuantity = 4
        };
        var userCart = CreateCart(userId, product.Id, quantity: 2);
        userCart.Items.Single().Product = product;
        var guestCart = new Cart
        {
            Id = Guid.NewGuid(),
            SessionId = guestSessionId
        };
        guestCart.Items.Add(new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = guestCart.Id,
            ProductId = product.Id,
            Product = product,
            Quantity = 3
        });

        _carts
            .Setup(x => x.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCart);
        _carts
            .Setup(x => x.GetActiveBySessionIdAsync(guestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guestCart);
        _products
            .Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _carts
            .Setup(x => x.GetActiveReservedQuantityForProductAsync(
                product.Id,
                userCart.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.MergeGuestCartIntoUserCartAsync(userId, guestSessionId));

        Assert.Contains("legfeljebb 4 db", exception.Message);
        Assert.False(guestCart.IsCheckedOut);
        Assert.Null(guestCart.CheckedOutAtUtc);
        Assert.Equal(2, userCart.Items.Single().Quantity);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CleanupExpiredCarts_RemovesAllExpiredCarts_AndSavesOnce()
    {
        var expired = new List<Cart> { new() { Id = Guid.NewGuid() }, new() { Id = Guid.NewGuid() } };
        _carts
            .Setup(x => x.GetExpiredCartsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expired);
        foreach (var cart in expired)
        {
            _carts.Setup(x => x.Remove(cart));
        }
        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expired.Count);

        var removedCount = await _service.CleanupExpiredCartsAsync();

        Assert.Equal(2, removedCount);
        foreach (var cart in expired)
        {
            _carts.Verify(x => x.Remove(cart), Times.Once);
        }
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Cart CreateCart(Guid userId, int productId, int quantity)
    {
        var cart = new Cart { Id = Guid.NewGuid(), UserId = userId };
        cart.Items.Add(new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = cart.Id,
            ProductId = productId,
            Quantity = quantity
        });
        return cart;
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
