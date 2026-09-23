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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class OrderServiceTests
{
    private const string IdempotencyKey = "testorderkey123";

    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);
    private readonly Mock<IOrderRepository> _orders = new(MockBehavior.Strict);
    private readonly Mock<IOrderEmailRepository> _orderEmails = new(MockBehavior.Strict);
    private readonly Mock<ICartRepository> _carts = new(MockBehavior.Strict);
    private readonly Mock<IProductRepository> _products = new(MockBehavior.Strict);
    private readonly Mock<ICartService> _cartService = new(MockBehavior.Strict);
    private readonly Mock<IEmailService> _emailService = new(MockBehavior.Strict);
    private readonly Mock<IEmailTemplateService> _emailTemplateService = new(MockBehavior.Strict);
    private readonly Mock<ISumUpCheckoutService> _sumUpCheckoutService = new(MockBehavior.Strict);
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 7, 24, 8, 0, 0, TimeSpan.Zero));
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _unitOfWork.SetupGet(x => x.OrderRepository).Returns(_orders.Object);
        _unitOfWork.SetupGet(x => x.OrderEmailRepository).Returns(_orderEmails.Object);
        _unitOfWork.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<OrderWithdrawalRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<OrderWithdrawalRequestDto>> action, CancellationToken token) => action(token));
        _orderEmails.Setup(x => x.Add(It.IsAny<OrderEmail>()));
        _unitOfWork.SetupGet(x => x.ProductRepository).Returns(_products.Object);
        _unitOfWork
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<OrderDto>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<OrderDto>>, CancellationToken>(
                (operation, token) => operation(token));

        var userStore = new Mock<IUserStore<ApplicationUser>>(MockBehavior.Strict);
        _userManager = CreateUserManagerMock(userStore.Object);
        _userManager
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        _service = new OrderService(
            _unitOfWork.Object,
            _cartService.Object,
            _userManager.Object,
            _emailService.Object,
            _emailTemplateService.Object,
            _sumUpCheckoutService.Object,
            _timeProvider,
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
        var insideTransaction = false;
        var queuedEmails = new List<OrderEmail>();
        _orderEmails.Setup(x => x.Add(It.IsAny<OrderEmail>()))
            .Callback<OrderEmail>(email =>
            {
                Assert.True(insideTransaction);
                queuedEmails.Add(email);
            });
        _unitOfWork.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<OrderDto>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<OrderDto>>, CancellationToken>(async (operation, token) =>
            {
                insideTransaction = true;
                var result = await operation(token);
                Assert.Equal(2, queuedEmails.Count);
                insideTransaction = false;
                return result;
            });
        const string sessionId = "guest-session";
        var product = new Product { Id = 4, Name = "Bazsalikom", Available = true, StockQuantity = 3, Price = 900 };
        var cart = new Cart { Id = Guid.NewGuid(), SessionId = sessionId };
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Product = product, Quantity = 2 });
        Order? savedOrder = null;
        _unitOfWork.SetupGet(x => x.CartRepository).Returns(_carts.Object);
        _orders
            .Setup(x => x.GetByIdempotencyKeyWithItemsAsync(IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
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
            .ThrowsAsync(new System.Net.Mail.SmtpException("SMTP unavailable"));
        _emailService
            .Setup(x => x.SendAsync(businessMessage, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var resultOutcome = await _service.CreateOrderFromCartAsync(null, sessionId, new PlaceOrderDto
        {
            IdempotencyKey = IdempotencyKey,
            CustomerName = " Teszt Elek ",
            CustomerEmail = " guest@example.com ",
            CustomerPhone = " +361234567 ",
            PostalCode = "8200",
            City = "Veszprém",
            Street = "Kert utca",
            HouseNumber = "1",
            AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
            AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
            FulfillmentMethod = FulfillmentMethod.PersonalPickup
        });
        var result = resultOutcome.Order;

        Assert.Null(result.UserId);
        Assert.Null(result.AddressId);
        Assert.StartsWith("BK-20260724-", result.OrderNumber, StringComparison.Ordinal);
        Assert.Equal("Teszt Elek", result.CustomerName);
        Assert.Equal("guest@example.com", result.CustomerEmail);
        Assert.Equal("+361234567", result.CustomerPhone);
        Assert.Equal(LegalDocumentVersions.CurrentTermsVersion, result.AcceptedTermsVersion);
        Assert.Equal(LegalDocumentVersions.CurrentPrivacyPolicyVersion, result.AcceptedPrivacyPolicyVersion);
        Assert.True(cart.IsCheckedOut);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, cart.CheckedOutAtUtc);
        Assert.NotNull(savedOrder);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, savedOrder.CreatedAtUtc);
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
        _emailService.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _orderEmails.Verify(x => x.Add(It.Is<OrderEmail>(email =>
            email.OrderId == result.Id && email.Kind == "customer" && email.Recipient == customerMessage.To)), Times.Once);
        _orderEmails.Verify(x => x.Add(It.Is<OrderEmail>(email =>
            email.OrderId == result.Id && email.Kind == "business" && email.Recipient == businessMessage.To)), Times.Once);
        _unitOfWork.Verify(
            x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<OrderDto>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_WhenEmailQueueCannotBeSaved_DoesNotCompleteTransaction()
    {
        const string sessionId = "guest-session";
        var product = new Product { Id = 4, Name = "Bazsalikom", Available = true, StockQuantity = 3, Price = 900 };
        var cart = new Cart { Id = Guid.NewGuid(), SessionId = sessionId };
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Product = product, Quantity = 1 });
        SetupGuestOrderCreation(sessionId, cart, product, _ => { });
        var transactionCompleted = false;
        _unitOfWork.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<OrderDto>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<OrderDto>>, CancellationToken>(async (operation, token) =>
            {
                var result = await operation(token);
                transactionCompleted = true;
                return result;
            });
        _unitOfWork.SetupSequence(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .ThrowsAsync(new InvalidOperationException("Cannot persist email queue"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateOrderFromCartAsync(null, sessionId, new PlaceOrderDto
        {
            IdempotencyKey = IdempotencyKey,
            CustomerName = "Teszt Elek",
            CustomerEmail = "guest@example.com",
            CustomerPhone = "+361234567",
            AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
            AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
            FulfillmentMethod = FulfillmentMethod.PersonalPickup
        }));

        Assert.False(transactionCompleted);
        _orderEmails.Verify(x => x.Add(It.IsAny<OrderEmail>()), Times.Exactly(2));
        _emailService.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_AsGuestWithConfirmedAccountEmail_KeepsSessionOwnershipForRetryAndPayment()
    {
        const string sessionId = "guest-session";
        var userId = Guid.NewGuid();
        var product = new Product { Id = 4, Name = "Bazsalikom", Available = true, StockQuantity = 3, Price = 900 };
        var cart = new Cart { Id = Guid.NewGuid(), SessionId = sessionId };
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Product = product, Quantity = 2 });
        Order? savedOrder = null;
        SetupGuestOrderCreation(sessionId, cart, product, order => savedOrder = order);
        _userManager
            .Setup(x => x.FindByEmailAsync("guest@example.com"))
            .ReturnsAsync(new ApplicationUser
            {
                Id = userId,
                Email = "guest@example.com",
                EmailConfirmed = true,
                RecordStatus = RecordStatus.Active
            });

        var request = new PlaceOrderDto
        {
            IdempotencyKey = IdempotencyKey,
            CustomerName = "Teszt Elek",
            CustomerEmail = "guest@example.com",
            CustomerPhone = "+361234567",
            AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
            AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
            FulfillmentMethod = FulfillmentMethod.PersonalPickup
        };
        var resultOutcome = await _service.CreateOrderFromCartAsync(null, sessionId, request);
        var result = resultOutcome.Order;

        Assert.NotNull(savedOrder);
        Assert.Null(result.UserId);
        Assert.Null(savedOrder.UserId);
        Assert.Equal("guest@example.com", result.CustomerEmail);

        // Simulate reloading the persisted order with its original cart.
        savedOrder.Cart = cart;
        _orders.Setup(x => x.GetByIdempotencyKeyWithItemsAsync(IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedOrder);
        var retriedOrderOutcome = await _service.CreateOrderFromCartAsync(null, sessionId, request);
        var retriedOrder = retriedOrderOutcome.Order;
        Assert.True(resultOutcome.Created);
        Assert.False(retriedOrderOutcome.Created);
        Assert.Equal(result.Id, retriedOrder.Id);
        _orders.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);

        _orders.Setup(x => x.GetByIdWithItemsAndCartAsync(result.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedOrder);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateSumUpCheckoutAsync(null, "different-session", result.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateSumUpCheckoutAsync(userId, null, result.Id));
        _sumUpCheckoutService.VerifyNoOtherCalls();

        _sumUpCheckoutService.Setup(x => x.CreateCheckoutAsync(
                It.Is<OrderDto>(dto => dto.Id == result.Id && dto.UserId == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SumUpCheckoutResult(
                "checkout_123", "PENDING", "https://checkout.sumup.com/pay/checkout_123", "txn_123"));
        _orders.Setup(x => x.Update(savedOrder));

        var checkout = await _service.CreateSumUpCheckoutAsync(null, sessionId, result.Id);

        Assert.Equal(result.Id, checkout.OrderId);
        Assert.Equal("checkout_123", checkout.CheckoutId);
        _userManager.Verify(x => x.FindByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_AsAuthenticatedCustomer_KeepsAuthenticatedOwnership()
    {
        var userId = Guid.NewGuid();
        var product = new Product { Id = 4, Name = "Bazsalikom", Available = true, StockQuantity = 3, Price = 900 };
        var cart = new Cart { Id = Guid.NewGuid(), UserId = userId };
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Product = product, Quantity = 1 });
        Order? savedOrder = null;
        SetupGuestOrderCreation("unused-session", cart, product, order => savedOrder = order);
        _carts.Setup(x => x.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _cartService.Setup(x => x.ValidateCartForCheckoutAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Biogarden.BusinessLogic.DTOs.Carts.CartValidationResultDto { IsValid = true });
        _userManager.Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(new ApplicationUser
            {
                Id = userId,
                Email = "account@example.com",
                CustomerType = CustomerType.Private
            });

        var resultOutcome = await _service.CreateOrderFromCartAsync(userId, null, new PlaceOrderDto
        {
            IdempotencyKey = IdempotencyKey,
            CustomerName = "Teszt Elek",
            CustomerEmail = "guest@example.com",
            CustomerPhone = "+361234567",
            AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
            AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
            FulfillmentMethod = FulfillmentMethod.PersonalPickup
        });
        var result = resultOutcome.Order;

        Assert.NotNull(savedOrder);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(userId, savedOrder.UserId);
        Assert.Equal("guest@example.com", result.CustomerEmail);
        _userManager.Verify(x => x.FindByEmailAsync(It.IsAny<string>()), Times.Never);
        _carts.Verify(x => x.GetActiveBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_AsCompanyGuest_SnapshotsCompanyBillingAndPrintedInvoiceRequest()
    {
        const string sessionId = "guest-session";
        var product = new Product { Id = 4, Name = "Bazsalikom", Available = true, StockQuantity = 3, Price = 900 };
        var cart = new Cart { Id = Guid.NewGuid(), SessionId = sessionId };
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Product = product, Quantity = 2 });
        Order? savedOrder = null;
        SetupGuestOrderCreation(sessionId, cart, product, order => savedOrder = order);

        var resultOutcome = await _service.CreateOrderFromCartAsync(null, sessionId, new PlaceOrderDto
        {
            IdempotencyKey = IdempotencyKey,
            CustomerName = "Teszt Elek",
            CustomerEmail = "guest@example.com",
            CustomerPhone = "+361234567",
            CustomerType = CustomerType.Company,
            BillingCompanyName = "Teszt Kft.",
            BillingTaxNumber = "12345678-1-12",
            PrintedInvoiceRequested = true,
            PostalCode = "8200",
            City = "Veszprém",
            Street = "Kert utca",
            HouseNumber = "1",
            AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
            AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
            FulfillmentMethod = FulfillmentMethod.PersonalPickup
        });
        var result = resultOutcome.Order;

        Assert.NotNull(savedOrder);
        Assert.Equal(CustomerType.Company, result.CustomerType);
        Assert.Equal("Teszt Kft.", result.BillingCompanyName);
        Assert.Equal("12345678-1-12", result.BillingTaxNumber);
        Assert.True(result.PrintedInvoiceRequested);
        Assert.Equal("8200", result.PostalCode);
        Assert.Equal("Veszprém", result.City);
        Assert.Equal("Kert utca", result.Street);
        Assert.Equal("1", result.HouseNumber);
        Assert.Null(result.AddressId);
        Assert.Equal(CustomerType.Company, savedOrder.CustomerType);
        Assert.True(savedOrder.PrintedInvoiceRequested);
        Assert.Equal(1800, result.TotalPrice);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_AsPrivateGuest_IgnoresCompanyBillingAndPrintedInvoiceFields()
    {
        const string sessionId = "guest-session";
        var product = new Product { Id = 4, Name = "Bazsalikom", Available = true, StockQuantity = 3, Price = 900 };
        var cart = new Cart { Id = Guid.NewGuid(), SessionId = sessionId };
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Product = product, Quantity = 1 });
        Order? savedOrder = null;
        SetupGuestOrderCreation(sessionId, cart, product, order => savedOrder = order);

        var resultOutcome = await _service.CreateOrderFromCartAsync(null, sessionId, new PlaceOrderDto
        {
            IdempotencyKey = IdempotencyKey,
            CustomerName = "Teszt Elek",
            CustomerEmail = "guest@example.com",
            CustomerPhone = "+361234567",
            CustomerType = CustomerType.Private,
            BillingCompanyName = "Nem Menthető Kft.",
            BillingTaxNumber = "12345678-1-12",
            PrintedInvoiceRequested = true,
            AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
            AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
            FulfillmentMethod = FulfillmentMethod.PersonalPickup
        });
        var result = resultOutcome.Order;

        Assert.NotNull(savedOrder);
        Assert.Equal(CustomerType.Private, result.CustomerType);
        Assert.Null(result.BillingCompanyName);
        Assert.Null(result.BillingTaxNumber);
        Assert.False(result.PrintedInvoiceRequested);
        Assert.Null(result.AddressId);
        Assert.Equal(string.Empty, result.PostalCode);
        Assert.Equal(string.Empty, result.City);
        Assert.Equal(string.Empty, result.Street);
        Assert.Equal(string.Empty, result.HouseNumber);
        Assert.Null(savedOrder.BillingCompanyName);
        Assert.Null(savedOrder.BillingTaxNumber);
        Assert.False(savedOrder.PrintedInvoiceRequested);
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
            CheckedOutAtUtc = _timeProvider.GetUtcNow().UtcDateTime
        };
        var order = CreateOrder(null);
        order.CartId = cart.Id;
        order.CustomerEmail = "guest@example.com";

        _unitOfWork.SetupGet(x => x.CartRepository).Returns(_carts.Object);
        _orders
            .Setup(x => x.GetByIdempotencyKeyWithItemsAsync(IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        _carts
            .Setup(x => x.GetActiveBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);
        _carts
            .Setup(x => x.GetLatestCheckedOutBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _orders
            .Setup(x => x.GetByCartIdWithItemsAsync(cart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var resultOutcome = await _service.CreateOrderFromCartAsync(null, sessionId, new PlaceOrderDto
        {
            IdempotencyKey = IdempotencyKey,
            CustomerName = "Teszt Elek",
            CustomerEmail = "guest@example.com",
            CustomerPhone = "+361234567",
            AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
            AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
            FulfillmentMethod = FulfillmentMethod.PersonalPickup
        });
        var result = resultOutcome.Order;

        Assert.Equal(order.Id, result.Id);
        Assert.Equal("guest@example.com", result.CustomerEmail);
        _orders.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_WhenIdempotencyKeyAlreadyExists_ReturnsExistingOrderWithoutSendingEmails()
    {
        const string sessionId = "guest-session";
        var order = CreateOrder(null);
        order.IdempotencyKey = IdempotencyKey;
        order.CustomerEmail = "guest@example.com";
        order.Cart = new Cart
        {
            Id = order.CartId,
            SessionId = sessionId,
            IsCheckedOut = true
        };

        _orders
            .Setup(x => x.GetByIdempotencyKeyWithItemsAsync(IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var resultOutcome = await _service.CreateOrderFromCartAsync(null, sessionId, new PlaceOrderDto
        {
            IdempotencyKey = IdempotencyKey,
            CustomerName = "Teszt Elek",
            CustomerEmail = "guest@example.com",
            CustomerPhone = "+361234567",
            AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
            AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
            FulfillmentMethod = FulfillmentMethod.PersonalPickup
        });
        var result = resultOutcome.Order;

        Assert.Equal(order.Id, result.Id);
        Assert.Equal(IdempotencyKey, result.IdempotencyKey);
        _orderEmails.Verify(x => x.Add(It.IsAny<OrderEmail>()), Times.Never);
        _carts.Verify(x => x.GetActiveBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_WhenIdempotencyKeyBelongsToDifferentGuestSession_Throws()
    {
        var order = CreateOrder(null);
        order.IdempotencyKey = IdempotencyKey;
        order.Cart = new Cart
        {
            Id = order.CartId,
            SessionId = "other-session",
            IsCheckedOut = true
        };

        _orders
            .Setup(x => x.GetByIdempotencyKeyWithItemsAsync(IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateOrderFromCartAsync(null, "guest-session", new PlaceOrderDto
            {
                IdempotencyKey = IdempotencyKey,
                CustomerName = "Teszt Elek",
                CustomerEmail = "guest@example.com",
                CustomerPhone = "+361234567",
                AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
                AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
                FulfillmentMethod = FulfillmentMethod.PersonalPickup
            }));

        _carts.Verify(x => x.GetActiveBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_WhenIdempotencyKeyIsMissing_ThrowsBeforeCheckout()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateOrderFromCartAsync(null, "guest-session", new PlaceOrderDto
            {
                CustomerName = "Teszt Elek",
                CustomerEmail = "guest@example.com",
                CustomerPhone = "+361234567",
                AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
                AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
                FulfillmentMethod = FulfillmentMethod.PersonalPickup
            }));

        Assert.Contains("azonosító kulcsa", exception.Message);
        _carts.Verify(x => x.GetActiveBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_WhenStockChangesDuringSave_ThrowsBeforeSendingEmails()
    {
        const string sessionId = "guest-session";
        var product = new Product { Id = 4, Name = "Bazsalikom", Available = true, StockQuantity = 1, Price = 900 };
        var cart = new Cart { Id = Guid.NewGuid(), SessionId = sessionId };
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Product = product, Quantity = 1 });

        _unitOfWork.SetupGet(x => x.CartRepository).Returns(_carts.Object);
        _orders
            .Setup(x => x.GetByIdempotencyKeyWithItemsAsync(IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
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
                IdempotencyKey = IdempotencyKey,
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
    public async Task CreateOrderFromCartAsync_WhenStockIsStaleAfterValidation_ThrowsBeforeSaving()
    {
        const string sessionId = "guest-session";
        var product = new Product { Id = 4, Name = "Bazsalikom", Available = true, StockQuantity = 0, Price = 900 };
        var cart = new Cart { Id = Guid.NewGuid(), SessionId = sessionId };
        cart.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, Product = product, Quantity = 1 });

        _unitOfWork.SetupGet(x => x.CartRepository).Returns(_carts.Object);
        _orders
            .Setup(x => x.GetByIdempotencyKeyWithItemsAsync(IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        _carts.Setup(x => x.GetActiveBySessionIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _cartService.Setup(x => x.ValidateCartForCheckoutAsync(null, sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Biogarden.BusinessLogic.DTOs.Carts.CartValidationResultDto { IsValid = true });
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateOrderFromCartAsync(null, sessionId, new PlaceOrderDto
            {
                IdempotencyKey = IdempotencyKey,
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
        Assert.Equal(0, product.StockQuantity);
        _orders.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        _orders
            .Setup(x => x.GetByIdempotencyKeyWithItemsAsync(IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        _carts.Setup(x => x.GetActiveBySessionIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateOrderFromCartAsync(null, sessionId, new PlaceOrderDto
            {
                IdempotencyKey = IdempotencyKey,
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
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, order.ConfirmedAtUtc);
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

    [Theory]
    [InlineData(PaymentStatus.Pending, PaymentStatus.RefundPending)]
    [InlineData(PaymentStatus.RefundPending, PaymentStatus.RefundPending)]
    [InlineData(PaymentStatus.Refunded, PaymentStatus.Refunded)]
    public async Task ConfirmSumUpCheckoutAsync_AfterCancellation_PreservesCancellationAndReconcilesPayment(
        PaymentStatus initial, PaymentStatus expected)
    {
        var order = CreateOrder(null);
        order.Status = OrderStatus.Cancelled;
        order.StockCommitted = false;
        order.CancelledAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        order.PaymentStatus = initial;
        order.TotalPrice = 3600;
        _orders.Setup(x => x.GetBySumUpCheckoutIdAsync("checkout_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orders.Setup(x => x.Update(order));
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = await _service.ConfirmSumUpCheckoutAsync("checkout_123", "txn_123", 3600, "HUF");
            Assert.Equal(OrderStatus.Cancelled, result.Status);
            Assert.Equal(expected, result.PaymentStatus);
            Assert.False(order.StockCommitted);
            Assert.Equal("txn_123", order.SumUpTransactionId);
            Assert.NotNull(order.CancelledAtUtc);
        }
        await _service.MarkSumUpPaymentFailedAsync("checkout_123", null);
        Assert.Equal(expected, order.PaymentStatus);
    }

    [Fact]
    public async Task ConfirmSumUpCheckoutAsync_WhenCurrencyDoesNotMatch_ThrowsAndDoesNotSave()
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
                3600,
                "EUR"));

        Assert.Contains("currency", exception.Message);
        _orders.Verify(x => x.Update(It.IsAny<Order>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkSumUpPaymentFailedAsync_WhenOrderIsPending_MarksFailedAndStoresTransactionId()
    {
        var order = CreateOrder(null);
        order.SumUpCheckoutId = "checkout_123";

        _orders
            .Setup(x => x.GetBySumUpCheckoutIdAsync("checkout_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orders.Setup(x => x.Update(order));
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.MarkSumUpPaymentFailedAsync("checkout_123", "txn_123");

        Assert.NotNull(result);
        Assert.Equal(PaymentStatus.Failed, result.PaymentStatus);
        Assert.Equal(OrderStatus.Pending, result.Status);
        Assert.Equal("txn_123", order.SumUpTransactionId);
        _orders.Verify(x => x.Update(order), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkSumUpPaymentFailedAsync_WhenOrderIsAlreadyPaid_DoesNotDowngradePaymentStatus()
    {
        var order = CreateOrder(null);
        order.SumUpCheckoutId = "checkout_123";
        order.PaymentStatus = PaymentStatus.Paid;
        order.Status = OrderStatus.Confirmed;

        _orders
            .Setup(x => x.GetBySumUpCheckoutIdAsync("checkout_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orders.Setup(x => x.Update(order));
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.MarkSumUpPaymentFailedAsync("checkout_123", "txn_late_failure");

        Assert.NotNull(result);
        Assert.Equal(PaymentStatus.Paid, result.PaymentStatus);
        Assert.Equal(OrderStatus.Confirmed, result.Status);
        Assert.Equal("txn_late_failure", order.SumUpTransactionId);
        _orders.Verify(x => x.Update(order), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, order.CancelledAtUtc);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, order.DeletedAtUtc);
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
    public async Task RequestWithdrawalAsync_StoresRequestAndQueuesNotifications()
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
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, result.RequestedAtUtc);
        Assert.Equal(OrderWithdrawalRequestStatus.Pending, result.Status);
        Assert.Same(savedRequest, order.WithdrawalRequests.Single());
        _orderEmails.Verify(x => x.Add(It.Is<OrderEmail>(email => email.Kind == "withdrawal-business" && email.EventId != Guid.Empty && email.Recipient == businessMessage.To)), Times.Once);
        _orderEmails.Verify(x => x.Add(It.Is<OrderEmail>(email => email.Kind == "withdrawal-customer" && email.EventId != Guid.Empty && email.Recipient == customerMessage.To)), Times.Once);
    }

    [Fact]
    public async Task RequestWithdrawalByOrderLookupAsync_ForGuestOrder_StoresRequestAndQueuesNotifications()
    {
        var order = CreateOrder(null);
        order.CustomerName = "Vendég Vásárló";
        order.CustomerEmail = "guest@example.com";
        var businessMessage = new EmailMessage { To = "info@bakonykincse.hu", Subject = "Új kérés" };
        var customerMessage = new EmailMessage { To = "guest@example.com", Subject = "Kérés rögzítve" };
        OrderWithdrawalRequest? savedRequest = null;

        _orders
            .Setup(x => x.GetByOrderNumberAndEmailWithItemsAsync(
                "BK-TEST-0001",
                "guest@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orders
            .Setup(x => x.AddWithdrawalRequestAsync(It.IsAny<OrderWithdrawalRequest>(), It.IsAny<CancellationToken>()))
            .Callback<OrderWithdrawalRequest, CancellationToken>((request, _) =>
            {
                request.Id = 13;
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

        var result = await _service.RequestWithdrawalByOrderLookupAsync(new PublicOrderWithdrawalRequestDto
        {
            OrderNumber = "bk-test-0001",
            CustomerEmail = "GUEST@example.com",
            Reason = "Szeretnék elállási kérelmet rögzíteni."
        });

        Assert.Equal(13, result.Id);
        Assert.Equal(order.Id, result.OrderId);
        Assert.NotNull(savedRequest);
        Assert.Null(savedRequest.UserId);
        Assert.Equal("Szeretnék elállási kérelmet rögzíteni.", result.Reason);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, result.RequestedAtUtc);
        Assert.Equal(OrderWithdrawalRequestStatus.Pending, result.Status);
        _orderEmails.Verify(x => x.Add(It.Is<OrderEmail>(email => email.Kind == "withdrawal-business" && email.EventId != Guid.Empty && email.Recipient == businessMessage.To)), Times.Once);
        _orderEmails.Verify(x => x.Add(It.Is<OrderEmail>(email => email.Kind == "withdrawal-customer" && email.EventId != Guid.Empty && email.Recipient == customerMessage.To)), Times.Once);
    }

    [Fact]
    public async Task GetPublicOrderPaymentStatusAsync_WhenCheckoutReferenceMatches_ReturnsLimitedStatus()
    {
        var order = CreateOrder(null);
        order.Status = OrderStatus.Confirmed;
        order.PaymentStatus = PaymentStatus.Paid;
        order.ConfirmedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        order.CustomerEmail = "guest@example.com";
        order.CustomerPhone = "+36301234567";

        _orders
            .Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _service.GetPublicOrderPaymentStatusAsync(
            order.Id,
            order.Id.ToString("N"));

        Assert.Equal(order.Id, result.OrderId);
        Assert.Equal(order.OrderNumber, result.OrderNumber);
        Assert.Equal(OrderStatus.Confirmed, result.Status);
        Assert.Equal(PaymentStatus.Paid, result.PaymentStatus);
        Assert.Equal(order.ConfirmedAtUtc, result.ConfirmedAtUtc);
    }

    [Fact]
    public async Task GetPublicOrderPaymentStatusAsync_WhenCheckoutReferenceDoesNotMatch_ThrowsWithoutLoadingOrder()
    {
        var orderId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.GetPublicOrderPaymentStatusAsync(orderId, Guid.NewGuid().ToString("N")));

        _orders.Verify(
            x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
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

    private void SetupGuestOrderCreation(
        string sessionId,
        Cart cart,
        Product product,
        Action<Order> captureOrder)
    {
        Order? capturedOrder = null;
        _unitOfWork.SetupGet(x => x.CartRepository).Returns(_carts.Object);
        _orders
            .Setup(x => x.GetByIdempotencyKeyWithItemsAsync(IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        _carts
            .Setup(x => x.GetActiveBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _cartService
            .Setup(x => x.ValidateCartForCheckoutAsync(null, sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Biogarden.BusinessLogic.DTOs.Carts.CartValidationResultDto { IsValid = true });
        _products
            .Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _orders
            .Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) =>
            {
                capturedOrder = order;
                captureOrder(order);
            })
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _orders
            .Setup(x => x.GetByIdWithItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => capturedOrder);
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
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock(
        IUserStore<ApplicationUser> userStore) =>
        new(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            // Identity 10 resolves an optional metrics factory during construction.
            Mock.Of<IServiceProvider>(provider =>
                provider.GetService(typeof(System.Diagnostics.Metrics.IMeterFactory)) == null, MockBehavior.Strict),
            NullLogger<UserManager<ApplicationUser>>.Instance);
}
