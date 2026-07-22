using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Constants;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Models.Payments;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class OrderServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);
    private readonly Mock<IOrderRepository> _orders = new(MockBehavior.Strict);
    private readonly Mock<ICartRepository> _carts = new(MockBehavior.Strict);
    private readonly Mock<IProductRepository> _products = new(MockBehavior.Strict);
    private readonly Mock<ICartService> _cartService = new(MockBehavior.Strict);
    private readonly Mock<IEmailService> _emailService = new(MockBehavior.Strict);
    private readonly Mock<IEmailTemplateService> _emailTemplateService = new(MockBehavior.Strict);
    private readonly Mock<ISumUpCheckoutService> _sumUpCheckoutService = new(MockBehavior.Strict);
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _unitOfWork.SetupGet(x => x.OrderRepository).Returns(_orders.Object);
        _unitOfWork.SetupGet(x => x.ProductRepository).Returns(_products.Object);
        _unitOfWork
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<OrderDto>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<OrderDto>>, CancellationToken>(
                (operation, token) => operation(token));

        var userStore = new Mock<IUserStore<ApplicationUser>>(MockBehavior.Strict);
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _service = new OrderService(
            _unitOfWork.Object,
            _cartService.Object,
            userManager.Object,
            _emailService.Object,
            _emailTemplateService.Object,
            _sumUpCheckoutService.Object,
            Options.Create(new EmailBrandingOptions
            {
                ContactEmail = "info@bakonykincse.hu"
            }),
            Options.Create(new SumUpOptions
            {
                Currency = "HUF"
            }));
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
        var customerMessage = new EmailMessage
        {
            To = "guest@example.com",
            Subject = "Rendelésedet megkaptuk"
        };
        var businessMessage = new EmailMessage
        {
            To = "info@bakonykincse.hu",
            Subject = "Új rendelés érkezett"
        };
        _emailTemplateService
            .Setup(x => x.BuildOrderConfirmationMessage("guest@example.com", It.IsAny<OrderDto>()))
            .Returns(customerMessage);
        _emailTemplateService
            .Setup(x => x.BuildOrderBusinessNotificationMessage("info@bakonykincse.hu", It.IsAny<OrderDto>()))
            .Returns(businessMessage);
        _emailService
            .Setup(x => x.SendAsync(customerMessage, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _emailService
            .Setup(x => x.SendAsync(businessMessage, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CreateOrderFromCartAsync(null, sessionId, new PlaceOrderDto
        {
            CustomerName = "Teszt Elek",
            CustomerEmail = "guest@example.com",
            CustomerPhone = "+361234567",
            PostalCode = "8200",
            City = "Veszprém",
            Street = "Kert utca",
            HouseNumber = "1",
            AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
            AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
            FulfillmentMethod = FulfillmentMethod.PersonalPickup
        });

        Assert.Null(result.UserId);
        Assert.Null(result.AddressId);
        Assert.Equal("guest@example.com", result.CustomerEmail);
        Assert.Equal(LegalDocumentVersions.CurrentTermsVersion, result.AcceptedTermsVersion);
        Assert.Equal(LegalDocumentVersions.CurrentPrivacyPolicyVersion, result.AcceptedPrivacyPolicyVersion);
        Assert.True(cart.IsCheckedOut);
        _emailTemplateService.Verify(
            x => x.BuildOrderConfirmationMessage(
                "guest@example.com",
                It.Is<OrderDto>(order => order.OrderNumber == result.OrderNumber)),
            Times.Once);
        _emailTemplateService.Verify(
            x => x.BuildOrderBusinessNotificationMessage(
                "info@bakonykincse.hu",
                It.Is<OrderDto>(order => order.OrderNumber == result.OrderNumber)),
            Times.Once);
        _emailService.Verify(x => x.SendAsync(customerMessage, It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(x => x.SendAsync(businessMessage, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(
            x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<OrderDto>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_WhenGuestCartAlreadyCheckedOut_ReturnsExistingOrder()
    {
        const string sessionId = "guest-session";
        var cart = new Cart
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            IsCheckedOut = true,
            CheckedOutAtUtc = DateTime.UtcNow
        };
        var order = CreateOrder(null);
        order.CartId = cart.Id;
        order.CustomerEmail = "guest@example.com";

        _unitOfWork.SetupGet(x => x.CartRepository).Returns(_carts.Object);
        _carts
            .Setup(x => x.GetActiveBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);
        _carts
            .Setup(x => x.GetLatestCheckedOutBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _orders
            .Setup(x => x.GetByCartIdWithItemsAsync(cart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _service.CreateOrderFromCartAsync(null, sessionId, new PlaceOrderDto
        {
            CustomerName = "Teszt Elek",
            CustomerEmail = "guest@example.com",
            CustomerPhone = "+361234567",
            AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
            AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
            FulfillmentMethod = FulfillmentMethod.PersonalPickup
        });

        Assert.Equal(order.Id, result.Id);
        Assert.Equal("guest@example.com", result.CustomerEmail);
        _orders.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_WhenStockChangesDuringSave_ThrowsBeforeSendingEmails()
    {
        const string sessionId = "guest-session";
        var product = new Product { Id = 4, Name = "Bazsalikom", Available = true, StockQuantity = 1, Price = 900 };
        var cart = new Cart { Id = Guid.NewGuid(), SessionId = sessionId };
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Product = product, Quantity = 1 });

        _unitOfWork.SetupGet(x => x.CartRepository).Returns(_carts.Object);
        _carts.Setup(x => x.GetActiveBySessionIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _cartService.Setup(x => x.ValidateCartForCheckoutAsync(null, sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Biogarden.BusinessLogic.DTOs.Carts.CartValidationResultDto { IsValid = true });
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _orders.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateOrderFromCartAsync(null, sessionId, new PlaceOrderDto
            {
                CustomerName = "Teszt Elek",
                CustomerEmail = "guest@example.com",
                CustomerPhone = "+361234567",
                PostalCode = "8200",
                City = "Veszprem",
                Street = "Kert utca",
                HouseNumber = "1",
                AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
                AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
                FulfillmentMethod = FulfillmentMethod.PersonalPickup
            }));

        Assert.Contains("készlet", exception.Message);
        _emailTemplateService.Verify(
            x => x.BuildOrderConfirmationMessage(It.IsAny<string>(), It.IsAny<OrderDto>()),
            Times.Never);
        _emailService.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_WhenLegalVersionsAreMissing_ThrowsBeforeCheckout()
    {
        const string sessionId = "guest-session";
        var product = new Product { Id = 4, Name = "Bazsalikom", Available = true, StockQuantity = 3, Price = 900 };
        var cart = new Cart { Id = Guid.NewGuid(), SessionId = sessionId };
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Product = product, Quantity = 1 });

        _unitOfWork.SetupGet(x => x.CartRepository).Returns(_carts.Object);
        _carts.Setup(x => x.GetActiveBySessionIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateOrderFromCartAsync(null, sessionId, new PlaceOrderDto
            {
                CustomerName = "Teszt Elek",
                CustomerEmail = "guest@example.com",
                CustomerPhone = "+361234567",
                FulfillmentMethod = FulfillmentMethod.PersonalPickup
            }));

        Assert.Contains("Általános Szerződési Feltételek", exception.Message);
        _cartService.Verify(
            x => x.ValidateCartForCheckoutAsync(
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _orders.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task CreateSumUpCheckoutAsync_AsGuestWithMatchingCartSession_StoresCheckoutId()
    {
        const string sessionId = "guest-session";
        var order = CreateOrder(null);
        order.Cart = new Cart
        {
            Id = order.CartId,
            SessionId = sessionId,
            IsCheckedOut = true
        };
        order.Items.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = 1,
            ProductName = "Levendula",
            Unit = UnitOfMeasure.Piece,
            UnitPrice = 1800,
            Quantity = 2,
            LineTotal = 3600
        });

        _orders
            .Setup(x => x.GetByIdWithItemsAndCartAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _sumUpCheckoutService
            .Setup(x => x.CreateCheckoutAsync(
                It.Is<OrderDto>(dto => dto.Id == order.Id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult(
                "checkout_123",
                "PENDING",
                "https://checkout.sumup.com/pay/checkout_123",
                "txn_123"));
        _orders.Setup(x => x.Update(order));
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.CreateSumUpCheckoutAsync(
            null,
            sessionId,
            order.Id);

        Assert.Equal(order.Id, result.OrderId);
        Assert.Equal("checkout_123", result.CheckoutId);
        Assert.Equal("https://checkout.sumup.com/pay/checkout_123", result.Url);
        Assert.Equal("checkout_123", order.SumUpCheckoutId);
        Assert.Equal("txn_123", order.SumUpTransactionId);
    }

    [Fact]
    public async Task ConfirmSumUpCheckoutAsync_MarksOrderPaidAndConfirmed()
    {
        var order = CreateOrder(null);
        order.SumUpCheckoutId = "checkout_123";
        order.TotalPrice = 3600;

        _orders
            .Setup(x => x.GetBySumUpCheckoutIdAsync("checkout_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orders.Setup(x => x.Update(order));
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.ConfirmSumUpCheckoutAsync(
            "checkout_123",
            "txn_123",
            3600,
            "HUF");

        Assert.Equal(PaymentStatus.Paid, result.PaymentStatus);
        Assert.Equal(OrderStatus.Confirmed, result.Status);
        Assert.Equal("txn_123", order.SumUpTransactionId);
        Assert.NotNull(order.ConfirmedAtUtc);
    }

    [Fact]
    public async Task ConfirmSumUpCheckoutAsync_WhenAmountDoesNotMatch_ThrowsAndDoesNotSave()
    {
        var order = CreateOrder(null);
        order.SumUpCheckoutId = "checkout_123";
        order.TotalPrice = 3600;

        _orders
            .Setup(x => x.GetBySumUpCheckoutIdAsync("checkout_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmSumUpCheckoutAsync(
                "checkout_123",
                "txn_123",
                3500,
                "HUF"));

        Assert.Contains("amount", exception.Message);
        _orders.Verify(x => x.Update(It.IsAny<Order>()), Times.Never);
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

    [Fact]
    public async Task RequestWithdrawalAsync_StoresRequestAndSendsNotifications()
    {
        var userId = Guid.NewGuid();
        var order = CreateOrder(userId);
        order.CustomerName = "Teszt Elek";
        order.CustomerEmail = "guest@example.com";
        var businessMessage = new EmailMessage { To = "info@bakonykincse.hu", Subject = "Új kérés" };
        var customerMessage = new EmailMessage { To = "guest@example.com", Subject = "Kérés rögzítve" };
        OrderWithdrawalRequest? savedRequest = null;

        _orders
            .Setup(x => x.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orders
            .Setup(x => x.AddWithdrawalRequestAsync(It.IsAny<OrderWithdrawalRequest>(), It.IsAny<CancellationToken>()))
            .Callback<OrderWithdrawalRequest, CancellationToken>((request, _) =>
            {
                request.Id = 12;
                savedRequest = request;
            })
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _emailTemplateService
            .Setup(x => x.BuildOrderWithdrawalBusinessNotificationMessage(
                "info@bakonykincse.hu",
                It.IsAny<OrderDto>(),
                It.IsAny<OrderWithdrawalRequestDto>()))
            .Returns(businessMessage);
        _emailTemplateService
            .Setup(x => x.BuildOrderWithdrawalCustomerConfirmationMessage(
                "guest@example.com",
                It.IsAny<OrderDto>(),
                It.IsAny<OrderWithdrawalRequestDto>()))
            .Returns(customerMessage);
        _emailService
            .Setup(x => x.SendAsync(businessMessage, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _emailService
            .Setup(x => x.SendAsync(customerMessage, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.RequestWithdrawalAsync(
            userId,
            order.Id,
            new CreateOrderWithdrawalRequestDto { Reason = "Szeretnék elállási kérelmet rögzíteni." });

        Assert.Equal(12, result.Id);
        Assert.Equal(order.Id, result.OrderId);
        Assert.Equal("Szeretnék elállási kérelmet rögzíteni.", result.Reason);
        Assert.Equal(OrderWithdrawalRequestStatus.Pending, result.Status);
        Assert.Same(savedRequest, order.WithdrawalRequests.Single());
        _emailService.Verify(x => x.SendAsync(businessMessage, It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(x => x.SendAsync(customerMessage, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Order CreateOrder(Guid? userId) => new()
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
