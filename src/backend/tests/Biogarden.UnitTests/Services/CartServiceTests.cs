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
        _carts.Setup(repository => repository.AcquireOwnerLockAsync(It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestOwnerLock());
        _products.Setup(repository => repository.GuardStockReservation(It.IsAny<Product>()));
        _carts.Setup(repository => repository.GetActiveReservedQuantitiesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        _unitOfWork.SetupGet(unitOfWork => unitOfWork.CartRepository).Returns(_carts.Object);
        _unitOfWork.SetupGet(unitOfWork => unitOfWork.ProductRepository).Returns(_products.Object);
        _service = new CartService(_unitOfWork.Object, _timeProvider);
    }

    [Theory]
    [InlineData(1, int.MaxValue)]
    [InlineData(int.MaxValue, 1)]
    public async Task AddItemAsync_WhenQuantityOverflows_RejectsWithoutChangingReservation(int current, int additional)
    {
        var userId = Guid.NewGuid();
        var cart = CreateCart(userId, 1, current);
        var expiry = cart.ExpiresAtUtc;
        _carts.Setup(repository => repository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _products.Setup(repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Product { Id = 1, Name = "Test", Available = true, StockQuantity = int.MaxValue });
        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddItemAsync(userId, null,
            new AddCartItemDto { ProductId = 1, Quantity = additional }));
        Assert.Equal(current, cart.Items.Single().Quantity);
        Assert.Equal(expiry, cart.ExpiresAtUtc);
        _products.Verify(repository => repository.GuardStockReservation(It.IsAny<Product>()), Times.Never);
        _unitOfWork.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Merge_WhenQuantityOverflows_PreservesBothCarts()
    {
        var userId = Guid.NewGuid();
        var userCart = CreateCart(userId, 1, 1);
        var guestCart = CreateCart(Guid.NewGuid(), 1, int.MaxValue);
        guestCart.UserId = null;
        guestCart.SessionId = "guest";
        _carts.Setup(repository => repository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(userCart);
        _carts.Setup(repository => repository.GetActiveBySessionIdAsync("guest", It.IsAny<CancellationToken>())).ReturnsAsync(guestCart);
        await Assert.ThrowsAsync<ArgumentException>(() => _service.MergeGuestCartIntoUserCartAsync(userId, "guest"));
        Assert.Equal(1, userCart.Items.Single().Quantity);
        Assert.Equal(int.MaxValue, guestCart.Items.Single().Quantity);
        Assert.False(guestCart.IsCheckedOut);
        _products.Verify(repository => repository.GuardStockReservation(It.IsAny<Product>()), Times.Never);
        _unitOfWork.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItemAsync_WithNonPositiveQuantity_ThrowsBeforeRepositoryAccess()
    {
        var addCartItem = new AddCartItemDto { ProductId = 1, Quantity = 0 };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddItemAsync(Guid.NewGuid(), null, addCartItem));

        _unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetCartAsync_ExtendsReservationByThirtyMinutes()
    {
        var userId = Guid.NewGuid();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var previousExpiry = now.AddMinutes(2);
        var cart = new Cart { Id = Guid.NewGuid(), UserId = userId, ExpiresAtUtc = previousExpiry };
        _carts.Setup(cartRepository => cartRepository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.GetCartAsync(userId, null);

        Assert.True(result.ExpiresAtUtc > previousExpiry);
        Assert.Equal(now, cart.LastActivityAtUtc);
        Assert.Equal(now.AddMinutes(30), result.ExpiresAtUtc);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAvailableQuantityToAddAsync_SubtractsOtherReservationsAndCurrentCartQuantity()
    {
        var userId = Guid.NewGuid();
        var product = new Product { Id = 8, Name = "Uborka", Available = true, StockQuantity = 12 };
        var cart = CreateCart(userId, product.Id, quantity: 3);
        _products.Setup(productRepository => productRepository.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _carts.Setup(cartRepository => cartRepository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _carts.Setup(cartRepository => cartRepository.GetActiveReservedQuantityForProductAsync(product.Id, cart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(4);

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
            .Setup(cartRepository => cartRepository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _products
            .Setup(productRepository => productRepository.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _carts
            .Setup(cartRepository => cartRepository.GetActiveReservedQuantityForProductAsync(product.Id, cart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _carts
            .Setup(cartRepository => cartRepository.AddItemAsync(It.IsAny<CartItem>(), It.IsAny<CancellationToken>()))
            .Callback<CartItem, CancellationToken>((item, _) => cart.Items.Add(item))
            .Returns(Task.CompletedTask);
        _carts
            .Setup(cartRepository => cartRepository.GetByIdWithItemsAsync(cart.Id, It.IsAny<CancellationToken>()))
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
        _carts.Verify(cartRepository => cartRepository.Update(It.IsAny<Cart>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateCartForCheckout_WhenCartIsMissing_ReturnsInvalidResult()
    {
        var userId = Guid.NewGuid();
        _carts
            .Setup(cartRepository => cartRepository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
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
            .Setup(cartRepository => cartRepository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _products
            .Setup(productRepository => productRepository.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _carts
            .Setup(cartRepository => cartRepository.GetActiveReservedQuantitiesAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(product.Id)), cart.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [product.Id] = 1 });

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
            .Setup(cartRepository => cartRepository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCart);
        _carts
            .Setup(cartRepository => cartRepository.GetActiveBySessionIdAsync(guestSessionId.Trim(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(guestCart);
        _products
            .Setup(productRepository => productRepository.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _carts
            .Setup(cartRepository => cartRepository.GetActiveReservedQuantityForProductAsync(product.Id, userCart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _carts
            .Setup(cartRepository => cartRepository.GetByIdWithItemsAsync(userCart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCart);

        _carts.Setup(cartRepository => cartRepository.GetActiveReservedQuantityForProductAsync(product.Id, userCart.Id, guestCart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await _service.MergeGuestCartIntoUserCartAsync(userId, guestSessionId);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        Assert.True(guestCart.IsCheckedOut);
        Assert.Equal(now, guestCart.CheckedOutAtUtc);
        Assert.Equal(now, userCart.LastActivityAtUtc);
        Assert.Equal(now.AddMinutes(30), userCart.ExpiresAtUtc);
        Assert.Equal(5, userCart.Items.Single().Quantity);
        Assert.Equal(5, result.TotalItemCount);
        Assert.Equal(4500, result.TotalPrice);
        _carts.Verify(cartRepository => cartRepository.AddItemAsync(It.IsAny<CartItem>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
            .Setup(cartRepository => cartRepository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCart);
        _carts
            .Setup(cartRepository => cartRepository.GetActiveBySessionIdAsync(guestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guestCart);
        _products
            .Setup(productRepository => productRepository.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _carts
            .Setup(cartRepository => cartRepository.GetActiveReservedQuantityForProductAsync(product.Id, userCart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _carts.Setup(cartRepository => cartRepository.GetActiveReservedQuantityForProductAsync(product.Id, userCart.Id, guestCart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.MergeGuestCartIntoUserCartAsync(userId, guestSessionId));

        Assert.Contains("legfeljebb 4 db", exception.Message);
        Assert.False(guestCart.IsCheckedOut);
        Assert.Null(guestCart.CheckedOutAtUtc);
        Assert.Equal(2, userCart.Items.Single().Quantity);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckoutReportsDeletedAndUnavailableProductsWithoutSaving()
    {
        var userId = Guid.NewGuid();
        var cart = CreateCart(userId, productId: 1, quantity: 1);
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = 2, Quantity = 1 });
        _carts.Setup(repository => repository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _products.Setup(repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);
        _products.Setup(repository => repository.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Product { Id = 2, Name = "Unavailable product", Available = false, StockQuantity = 10 });

        var result = await _service.ValidateCartForCheckoutAsync(userId, null);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Issues.Count);
        Assert.Contains(result.Issues, issue => issue.ProductId == 1 && issue.Message == "A termék már nem létezik.");
        Assert.Contains(result.Issues, issue => issue.ProductId == 2 && issue.Message == "A termék jelenleg nem elérhető.");
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmptyCartCannotBeCheckedOut()
    {
        var userId = Guid.NewGuid();
        _carts.Setup(repository => repository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Cart { Id = Guid.NewGuid(), UserId = userId });

        var result = await _service.ValidateCartForCheckoutAsync(userId, null);

        Assert.False(result.IsValid);
        Assert.Equal("A kosár üres.", Assert.Single(result.Issues).Message);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ZeroQuantityRemovesTheItemAndRefreshesTheReservation()
    {
        var userId = Guid.NewGuid();
        var cart = CreateCart(userId, productId: 1, quantity: 2);
        var itemId = Assert.Single(cart.Items).Id;
        _carts.Setup(repository => repository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _carts.Setup(repository => repository.GetByIdWithItemsAsync(cart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.UpdateItemQuantityAsync(userId, null, itemId, new UpdateCartItemQuantityDto { Quantity = 0 });

        Assert.Empty(cart.Items);
        Assert.Empty(result.Items);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime.AddMinutes(30), result.ExpiresAtUtc);
        _products.VerifyNoOtherCalls();
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnknownCartItemCannotBeChangedOrRemoved()
    {
        var userId = Guid.NewGuid();
        var cart = CreateCart(userId, productId: 1, quantity: 2);
        _carts.Setup(repository => repository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        var otherItemId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateItemQuantityAsync(userId, null, otherItemId, new UpdateCartItemQuantityDto { Quantity = 3 }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.RemoveItemAsync(userId, null, otherItemId));

        Assert.Equal(2, Assert.Single(cart.Items).Quantity);
        _unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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

    private sealed class TestOwnerLock : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
