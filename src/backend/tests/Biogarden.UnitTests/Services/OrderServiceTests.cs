using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class OrderServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);
    private readonly Mock<IOrderRepository> _orders = new(MockBehavior.Strict);
    private readonly Mock<ICartRepository> _carts = new(MockBehavior.Strict);
    private readonly Mock<IProductRepository> _products = new(MockBehavior.Strict);
    private readonly Mock<ICartService> _cartService = new(MockBehavior.Strict);
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _unitOfWork.SetupGet(x => x.OrderRepository).Returns(_orders.Object);
        _unitOfWork.SetupGet(x => x.ProductRepository).Returns(_products.Object);

        var userStore = new Mock<IUserStore<ApplicationUser>>(MockBehavior.Strict);
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _service = new OrderService(_unitOfWork.Object, _cartService.Object, userManager.Object);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_AsGuest_UsesSessionCartAndSnapshotsManualData()
    {
        const string sessionId = "guest-session";
        var product = new Product { Id = 4, Name = "Bazsalikom", Available = true, StockQuantity = 3, Price = 900 };
        var cart = new Cart { Id = Guid.NewGuid(), SessionId = sessionId };
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Product = product, Quantity = 2 });
        Order? savedOrder = null;
        _unitOfWork.SetupGet(x => x.CartRepository).Returns(_carts.Object);
        _carts.Setup(x => x.GetActiveBySessionIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _cartService.Setup(x => x.ValidateCartForCheckoutAsync(null, sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Biogarden.BusinessLogic.DTOs.Carts.CartValidationResultDto { IsValid = true });
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _orders.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => savedOrder = order).Returns(Task.CompletedTask);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _orders.Setup(x => x.GetByIdWithItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => savedOrder);

        var result = await _service.CreateOrderFromCartAsync(null, sessionId, new PlaceOrderDto
        {
            CustomerName = "Teszt Elek",
            CustomerEmail = "guest@example.com",
            CustomerPhone = "+361234567",
            PostalCode = "8200",
            City = "Veszprém",
            Street = "Kert utca",
            HouseNumber = "1",
            FulfillmentMethod = FulfillmentMethod.PersonalPickup
        });

        Assert.Null(result.UserId);
        Assert.Null(result.AddressId);
        Assert.Equal("guest@example.com", result.CustomerEmail);
        Assert.True(cart.IsCheckedOut);
    }

    [Fact]
    public async Task GetOrderByIdAsync_WhenOrderBelongsToAnotherUser_Throws()
    {
        var requestingUserId = Guid.NewGuid();
        var order = CreateOrder(Guid.NewGuid());
        _orders
            .Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.GetOrderByIdAsync(requestingUserId, order.Id));
    }

    [Fact]
    public async Task ConfirmOrderPaymentAsync_WhenAlreadyPaid_DoesNotSaveAgain()
    {
        var userId = Guid.NewGuid();
        var order = CreateOrder(userId);
        order.Status = OrderStatus.Confirmed;
        order.PaymentStatus = PaymentStatus.Paid;
        _orders
            .Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _service.ConfirmOrderPaymentAsync(userId, order.Id);

        Assert.Equal(PaymentStatus.Paid, result.PaymentStatus);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenPaid_RestoresStockAndMarksRefundPending()
    {
        var userId = Guid.NewGuid();
        var product = new Product
        {
            Id = 10,
            Name = "Levendulaolaj",
            Available = true,
            StockQuantity = 4
        };
        var order = CreateOrder(userId);
        order.PaymentStatus = PaymentStatus.Paid;
        order.StockCommitted = true;
        order.Items.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = product.Id,
            ProductName = product.Name,
            Quantity = 2,
            UnitPrice = 1800,
            Unit = UnitOfMeasure.Piece,
            LineTotal = 3600
        });

        _orders
            .SetupSequence(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order)
            .ReturnsAsync(order);
        _products
            .Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _orders.Setup(x => x.Update(order));
        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.CancelOrderAsync(
            userId, order.Id, new CancelOrderDto { Reason = "  Meggondoltam magam.  " });

        Assert.Equal(OrderStatus.Cancelled, result.Status);
        Assert.Equal(PaymentStatus.RefundPending, result.PaymentStatus);
        Assert.Equal(6, product.StockQuantity);
        Assert.False(order.StockCommitted);
        Assert.Equal("Meggondoltam magam.", order.CancellationReason);
        Assert.NotNull(order.CancelledAtUtc);
        Assert.NotNull(order.DeletedAtUtc);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenShipped_ThrowsAndDoesNotSave()
    {
        var userId = Guid.NewGuid();
        var order = CreateOrder(userId);
        order.Status = OrderStatus.Shipped;
        _orders
            .Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelOrderAsync(userId, order.Id, new CancelOrderDto()));

        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Order CreateOrder(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        OrderNumber = "BK-TEST-0001",
        UserId = userId,
        AddressId = 1,
        CartId = Guid.NewGuid(),
        Status = OrderStatus.Pending,
        PaymentStatus = PaymentStatus.Pending
    };
}
