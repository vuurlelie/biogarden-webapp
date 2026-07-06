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
    private readonly CartService _service;

    public CartServiceTests()
    {
        _unitOfWork.SetupGet(x => x.CartRepository).Returns(_carts.Object);
        _unitOfWork.SetupGet(x => x.ProductRepository).Returns(_products.Object);
        _service = new CartService(_unitOfWork.Object);
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
}
