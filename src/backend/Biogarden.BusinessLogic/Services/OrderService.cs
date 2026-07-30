using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.DTOs.Payments;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.BusinessLogic.Constants;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Biogarden.BusinessLogic.Options;

namespace Biogarden.BusinessLogic.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartService _cartService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly ISumUpCheckoutService _sumUpCheckoutService;
    private readonly TimeProvider _timeProvider;
    private readonly EmailBrandingOptions _emailBrandingOptions;
    private readonly SumUpOptions _sumUpOptions;

    public OrderService(
        IUnitOfWork unitOfWork,
        ICartService cartService,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        ISumUpCheckoutService sumUpCheckoutService,
        TimeProvider timeProvider,
        IOptions<EmailBrandingOptions> emailBrandingOptions,
        IOptions<SumUpOptions> sumUpOptions)
    {
        _unitOfWork = unitOfWork;
        _cartService = cartService;
        _userManager = userManager;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _sumUpCheckoutService = sumUpCheckoutService;
        _timeProvider = timeProvider;
        _emailBrandingOptions = emailBrandingOptions.Value;
        _sumUpOptions = sumUpOptions.Value;
    }

    public async Task<OrderDto> CreateOrderFromCartAsync(
        Guid? userId,
        string? sessionId,
        PlaceOrderDto dto,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = NormalizeIdempotencyKey(dto.IdempotencyKey);
        var existingOrderByKey = await _unitOfWork.OrderRepository.GetByIdempotencyKeyWithItemsAsync(
            idempotencyKey,
            cancellationToken);
        if (existingOrderByKey is not null)
        {
            EnsureCanAccessOrder(existingOrderByKey, userId, sessionId);
            return existingOrderByKey.ToDto();
        }

        var cart = userId.HasValue
            ? await _unitOfWork.CartRepository.GetActiveByUserIdAsync(userId.Value, cancellationToken)
            : string.IsNullOrWhiteSpace(sessionId)
                ? null
                : await _unitOfWork.CartRepository.GetActiveBySessionIdAsync(sessionId, cancellationToken);

        if (cart is null)
        {
            var existingOrder = await GetLatestCheckedOutOrderAsync(userId, sessionId, cancellationToken);
            if (existingOrder is not null)
            {
                return existingOrder.ToDto();
            }

            throw new InvalidOperationException("Cart not found.");
        }

        if (cart.IsCheckedOut)
        {
            var existingOrder = await _unitOfWork.OrderRepository.GetByCartIdWithItemsAsync(
                cart.Id,
                cancellationToken);
            if (existingOrder is not null)
            {
                return existingOrder.ToDto();
            }

            throw new InvalidOperationException("Cart already checked out.");
        }

        if (cart.Items.Count == 0)
        {
            throw new InvalidOperationException("Cart is empty.");
        }

        if (!Enum.IsDefined(dto.FulfillmentMethod))
        {
            throw new InvalidOperationException("Invalid fulfillment method.");
        }

        if (dto.FulfillmentMethod != FulfillmentMethod.PersonalPickup &&
            cart.Items.Any(item => item.Product?.DeliveryAvailable == false))
        {
            throw new InvalidOperationException(
                "The cart contains a product that is available only for personal pickup.");
        }

        ValidateAcceptedLegalVersions(dto);

        var validationResult = await _cartService.ValidateCartForCheckoutAsync(
            userId,
            sessionId,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException("Cart validation failed.");
        }

        var customerSnapshot = await BuildOrderCustomerSnapshotAsync(userId, dto, cancellationToken);
        var orderUserId = userId ?? await ResolveGuestOrderUserIdAsync(
            customerSnapshot.CustomerEmail,
            cancellationToken);

        OrderDto createdOrderDto;

        try
        {
            createdOrderDto = await _unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
            {
                var now = GetUtcNow();
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    OrderNumber = GenerateOrderNumber(),
                    IdempotencyKey = idempotencyKey,
                    UserId = orderUserId,
                    AddressId = customerSnapshot.AddressId,
                    CustomerName = customerSnapshot.CustomerName,
                    CustomerEmail = customerSnapshot.CustomerEmail,
                    CustomerPhone = customerSnapshot.CustomerPhone,
                    PostalCode = customerSnapshot.PostalCode,
                    City = customerSnapshot.City,
                    Street = customerSnapshot.Street,
                    HouseNumber = customerSnapshot.HouseNumber,
                    Building = customerSnapshot.Building,
                    Floor = customerSnapshot.Floor,
                    Door = customerSnapshot.Door,
                    Doorbell = customerSnapshot.Doorbell,
                    BillingCompanyName = customerSnapshot.BillingCompanyName,
                    BillingTaxNumber = customerSnapshot.BillingTaxNumber,
                    CustomerType = customerSnapshot.CustomerType,
                    PrintedInvoiceRequested = customerSnapshot.PrintedInvoiceRequested,
                    AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
                    AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion,
                    DeliveryPostalCode = customerSnapshot.DeliveryPostalCode,
                    DeliveryCity = customerSnapshot.DeliveryCity,
                    DeliveryStreet = customerSnapshot.DeliveryStreet,
                    DeliveryHouseNumber = customerSnapshot.DeliveryHouseNumber,
                    DeliveryBuilding = customerSnapshot.DeliveryBuilding,
                    DeliveryFloor = customerSnapshot.DeliveryFloor,
                    DeliveryDoor = customerSnapshot.DeliveryDoor,
                    DeliveryDoorbell = customerSnapshot.DeliveryDoorbell,
                    CartId = cart.Id,
                    CreatedAtUtc = now,
                    Status = OrderStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending,
                    FulfillmentMethod = dto.FulfillmentMethod
                };

                order.TotalPrice = await AddOrderItemsAndCommitStockAsync(
                    order,
                    cart.Items,
                    dto.FulfillmentMethod,
                    transactionToken);
                order.StockCommitted = true;

                cart.IsCheckedOut = true;
                cart.CheckedOutAtUtc = now;

                await _unitOfWork.OrderRepository.AddAsync(order, transactionToken);
                await _unitOfWork.SaveChangesAsync(transactionToken);

                var createdOrder = await ReloadOrderWithItemsAsync(
                    order.Id,
                    transactionToken);

                return createdOrder.ToDto();
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException(
                "A kosárban lévő készlet időközben megváltozott. Kérjük, ellenőrizd újra a kosarat.",
                exception);
        }
        catch (DbUpdateException exception)
        {
            var existingOrder = await _unitOfWork.OrderRepository.GetByIdempotencyKeyWithItemsAsync(
                idempotencyKey,
                cancellationToken);
            existingOrder ??= await GetLatestCheckedOutOrderAsync(userId, sessionId, cancellationToken);
            if (existingOrder is not null)
            {
                EnsureCanAccessOrder(existingOrder, userId, sessionId);
                return existingOrder.ToDto();
            }

            throw new InvalidOperationException(
                "A rendelés mentése nem sikerült. Kérjük, próbáld újra.",
                exception);
        }

        await SendOrderCreatedEmailsAsync(createdOrderDto, cancellationToken);

        return createdOrderDto;
    }

    public async Task<OrderDto> ConfirmOrderPaymentAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.OrderRepository.GetByIdWithItemsAsync(
            orderId,
            cancellationToken);

        if (order is null || order.UserId != userId)
        {
            throw new KeyNotFoundException("Order not found.");
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled order cannot be confirmed.");
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            return order.ToDto();
        }

        if (!order.StockCommitted)
        {
            await CommitOrderStockAsync(order, cancellationToken);
        }

        order.PaymentStatus = PaymentStatus.Paid;
        order.Status = OrderStatus.Confirmed;
        order.ConfirmedAtUtc = GetUtcNow();

        _unitOfWork.OrderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var confirmedOrder = await ReloadOrderWithItemsAsync(
            order.Id,
            cancellationToken);

        return confirmedOrder.ToDto();
    }

    public async Task<SumUpCheckoutDto> CreateSumUpCheckoutAsync(
        Guid? userId,
        string? sessionId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.OrderRepository.GetByIdWithItemsAndCartAsync(
            orderId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Order not found.");

        EnsureCanAccessOrder(order, userId, sessionId);

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled order cannot be paid.");
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            throw new InvalidOperationException("Order is already paid.");
        }

        var checkout = await _sumUpCheckoutService.CreateCheckoutAsync(
            order.ToDto(),
            cancellationToken);

        order.SumUpCheckoutId = checkout.CheckoutId;
        order.SumUpTransactionId = checkout.TransactionCode;
        order.PaymentStatus = PaymentStatus.Pending;

        _unitOfWork.OrderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SumUpCheckoutDto
        {
            OrderId = order.Id,
            CheckoutId = checkout.CheckoutId,
            Url = checkout.HostedCheckoutUrl!
        };
    }

    public async Task<OrderDto> ConfirmSumUpCheckoutAsync(
        string sumUpCheckoutId,
        string? sumUpTransactionId,
        decimal? amount = null,
        string? currency = null,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.OrderRepository.GetBySumUpCheckoutIdAsync(
            sumUpCheckoutId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Order not found.");

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled order cannot be confirmed.");
        }

        EnsureSumUpCheckoutMatchesOrder(order, amount, currency);

        if (order.PaymentStatus != PaymentStatus.Paid)
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.Status = OrderStatus.Confirmed;
            order.ConfirmedAtUtc ??= GetUtcNow();
        }

        order.SumUpTransactionId = NormalizeOptional(sumUpTransactionId) ?? order.SumUpTransactionId;

        _unitOfWork.OrderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.ToDto();
    }

    public async Task<OrderDto?> MarkSumUpPaymentFailedAsync(
        string sumUpCheckoutId,
        string? sumUpTransactionId,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.OrderRepository.GetBySumUpCheckoutIdAsync(
            sumUpCheckoutId,
            cancellationToken);

        if (order is null && !string.IsNullOrWhiteSpace(sumUpTransactionId))
        {
            order = await _unitOfWork.OrderRepository.GetBySumUpTransactionIdAsync(
                sumUpTransactionId,
                cancellationToken);
        }

        if (order is null)
        {
            return null;
        }

        if (order.PaymentStatus is not PaymentStatus.Paid and not PaymentStatus.Refunded)
        {
            order.PaymentStatus = PaymentStatus.Failed;
        }

        order.SumUpTransactionId = NormalizeOptional(sumUpTransactionId) ?? order.SumUpTransactionId;

        _unitOfWork.OrderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.ToDto();
    }

    public async Task<OrderDto> CancelOrderAsync(
        Guid userId,
        Guid orderId,
        CancelOrderDto dto,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.OrderRepository.GetByIdWithItemsAsync(
            orderId,
            cancellationToken);

        if (order is null || order.UserId != userId)
        {
            throw new KeyNotFoundException("Order not found.");
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            return order.ToDto();
        }

        if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Completed)
        {
            throw new InvalidOperationException("Order can no longer be cancelled.");
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            order.PaymentStatus = PaymentStatus.RefundPending;
        }

        if (order.StockCommitted)
        {
            await RestoreOrderStockAsync(order, cancellationToken);
        }

        order.Status = OrderStatus.Cancelled;
        order.CancelledAtUtc = GetUtcNow();
        order.DeletedAtUtc = order.CancelledAtUtc;
        order.CancellationReason = string.IsNullOrWhiteSpace(dto.Reason)
            ? null
            : dto.Reason.Trim();

        _unitOfWork.OrderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var cancelledOrder = await ReloadOrderWithItemsAsync(
            order.Id,
            cancellationToken);

        return cancelledOrder.ToDto();
    }

    public async Task<OrderWithdrawalRequestDto> RequestWithdrawalAsync(
        Guid userId,
        Guid orderId,
        CreateOrderWithdrawalRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.OrderRepository.GetByIdWithItemsAsync(
            orderId,
            cancellationToken);

        if (order is null || order.UserId != userId)
        {
            throw new KeyNotFoundException("Order not found.");
        }

        return await CreateWithdrawalRequestAsync(order, userId, dto.Reason, cancellationToken);
    }

    public async Task<OrderWithdrawalRequestDto> RequestWithdrawalByOrderLookupAsync(
        PublicOrderWithdrawalRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var orderNumber = NormalizeRequired(dto.OrderNumber, "A rendelés azonosítója", 3, 32).ToUpperInvariant();
        var customerEmail = NormalizeRequired(dto.CustomerEmail, "Az email cím", 5, 256).ToLowerInvariant();
        if (!System.Net.Mail.MailAddress.TryCreate(customerEmail, out _))
        {
            throw new ArgumentException("Az email cím formátuma hibás.");
        }

        var order = await _unitOfWork.OrderRepository.GetByOrderNumberAndEmailWithItemsAsync(
            orderNumber,
            customerEmail,
            cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException("Nem találtunk ilyen rendelést ezzel az email címmel.");
        }

        return await CreateWithdrawalRequestAsync(order, order.UserId, dto.Reason, cancellationToken);
    }

    private async Task<OrderWithdrawalRequestDto> CreateWithdrawalRequestAsync(
        Order order,
        Guid? userId,
        string reasonValue,
        CancellationToken cancellationToken)
    {
        if (order.WithdrawalRequests.Any(request => request.Status == OrderWithdrawalRequestStatus.Pending))
        {
            throw new InvalidOperationException("Ehhez a rendeléshez már van folyamatban lévő elállási vagy fogyasztói kérés.");
        }

        var reason = NormalizeRequired(reasonValue, "Az elállási vagy fogyasztói kérés leírása", 10, 1000);
        var request = new OrderWithdrawalRequest
        {
            OrderId = order.Id,
            UserId = userId,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            Reason = reason,
            RequestedAtUtc = GetUtcNow(),
            Status = OrderWithdrawalRequestStatus.Pending
        };

        await _unitOfWork.OrderRepository.AddWithdrawalRequestAsync(request, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        order.WithdrawalRequests.Add(request);
        var orderDto = order.ToDto();
        var requestDto = orderDto.WithdrawalRequests.First(item => item.Id == request.Id);

        await _emailService.SendAsync(
            _emailTemplateService.BuildOrderWithdrawalBusinessNotificationMessage(
                _emailBrandingOptions.ContactEmail,
                orderDto,
                requestDto),
            cancellationToken);
        await _emailService.SendAsync(
            _emailTemplateService.BuildOrderWithdrawalCustomerConfirmationMessage(
                order.CustomerEmail,
                orderDto,
                requestDto),
            cancellationToken);

        return requestDto;
    }

    public async Task<List<OrderDto>> GetMyOrdersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var orders = await _unitOfWork.OrderRepository.GetByUserIdAsync(
            userId,
            cancellationToken);

        var visibleFromUtc = GetUtcNow().AddDays(-7);

        return orders
            .Where(order => IsVisibleInUserHistory(order, visibleFromUtc))
            .Select(order => order.ToDto())
            .ToList();
    }

    public async Task<OrderDto> GetOrderByIdAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.OrderRepository.GetByIdWithItemsAsync(
            orderId,
            cancellationToken);

        if (order is null || order.UserId != userId ||
            !IsVisibleInUserHistory(order, GetUtcNow().AddDays(-7)))
        {
            throw new KeyNotFoundException("Order not found.");
        }

        return order.ToDto();
    }

    public async Task<PublicOrderPaymentStatusDto> GetPublicOrderPaymentStatusAsync(
        Guid orderId,
        string sumUpCheckoutReference,
        CancellationToken cancellationToken = default)
    {
        var normalizedCheckoutReference = NormalizeOptional(sumUpCheckoutReference);
        if (!string.Equals(normalizedCheckoutReference, orderId.ToString("N"), StringComparison.OrdinalIgnoreCase))
        {
            throw new KeyNotFoundException("Order not found.");
        }

        var order = await _unitOfWork.OrderRepository.GetByIdAsync(
            orderId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Order not found.");

        return new PublicOrderPaymentStatusDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            ConfirmedAtUtc = order.ConfirmedAtUtc
        };
    }

    private static bool IsVisibleInUserHistory(Order order, DateTime visibleFromUtc)
    {
        if (order.Status != OrderStatus.Cancelled)
        {
            return true;
        }

        var deletedAtUtc = order.DeletedAtUtc ?? order.CancelledAtUtc;
        return deletedAtUtc.HasValue && deletedAtUtc.Value >= visibleFromUtc;
    }

    private static void EnsureCanAccessOrder(Order order, Guid? userId, string? sessionId)
    {
        if (order.UserId.HasValue)
        {
            if (userId != order.UserId)
            {
                throw new KeyNotFoundException("Order not found.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(sessionId) || order.Cart?.SessionId != sessionId)
        {
            throw new KeyNotFoundException("Order not found.");
        }
    }

    private async Task<decimal> AddOrderItemsAndCommitStockAsync(
        Order order,
        IEnumerable<CartItem> cartItems,
        FulfillmentMethod fulfillmentMethod,
        CancellationToken cancellationToken)
    {
        decimal totalPrice = 0m;

        foreach (var cartItem in cartItems)
        {
            var product = await _unitOfWork.ProductRepository.GetByIdAsync(
                cartItem.ProductId,
                cancellationToken);

            if (product is null)
            {
                throw new InvalidOperationException("Product missing during order creation.");
            }

            if (!product.Available)
            {
                throw new InvalidOperationException("A termék időközben elérhetetlenné vált. Kérjük, ellenőrizd újra a kosarat.");
            }

            if (fulfillmentMethod != FulfillmentMethod.PersonalPickup &&
                !product.DeliveryAvailable)
            {
                throw new InvalidOperationException("A kosárban időközben személyes átvételes termék jelent meg. Kérjük, ellenőrizd újra a kosarat.");
            }

            if (product.StockQuantity < cartItem.Quantity)
            {
                throw new InvalidOperationException("A kosárban lévő készlet időközben megváltozott. Kérjük, ellenőrizd újra a kosarat.");
            }

            var lineTotal = product.Price * cartItem.Quantity;
            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Unit = product.Unit,
                Quantity = cartItem.Quantity,
                LineTotal = lineTotal
            });

            totalPrice += lineTotal;
            product.StockQuantity -= cartItem.Quantity;
        }

        return totalPrice;
    }

    private async Task CommitOrderStockAsync(Order order, CancellationToken cancellationToken)
    {
        foreach (var orderItem in order.Items)
        {
            var product = await _unitOfWork.ProductRepository.GetByIdAsync(
                orderItem.ProductId,
                cancellationToken);

            if (product is null)
            {
                throw new InvalidOperationException("Product missing during payment confirmation.");
            }

            product.StockQuantity -= orderItem.Quantity;
            if (product.StockQuantity < 0)
            {
                throw new InvalidOperationException("Stock inconsistency detected.");
            }
        }

        order.StockCommitted = true;
    }

    private async Task RestoreOrderStockAsync(Order order, CancellationToken cancellationToken)
    {
        foreach (var orderItem in order.Items)
        {
            var product = await _unitOfWork.ProductRepository.GetByIdAsync(
                orderItem.ProductId,
                cancellationToken);

            if (product is not null)
            {
                product.StockQuantity += orderItem.Quantity;
            }
        }

        order.StockCommitted = false;
    }

    private async Task<OrderCustomerSnapshot> BuildOrderCustomerSnapshotAsync(
        Guid? userId,
        PlaceOrderDto dto,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = userId.HasValue
            ? await _userManager.FindByIdAsync(userId.Value.ToString())
            : null;
        if (userId.HasValue && user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var useManualContact = !string.IsNullOrWhiteSpace(dto.CustomerName) ||
            !string.IsNullOrWhiteSpace(dto.CustomerEmail) ||
            !string.IsNullOrWhiteSpace(dto.CustomerPhone);
        var customerContact = BuildCustomerContact(user, useManualContact, dto);

        var customerType = user?.CustomerType ?? dto.CustomerType;
        if (!Enum.IsDefined(customerType))
        {
            throw new InvalidOperationException("Invalid customer type.");
        }

        var billingCompanyName = user is not null && !useManualContact
            ? user.CompanyName
            : dto.BillingCompanyName;
        var billingTaxNumber = user is not null && !useManualContact
            ? user.TaxNumber
            : dto.BillingTaxNumber;
        var printedInvoiceRequested = customerType == CustomerType.Company &&
            (user?.PrintedInvoiceRequested ?? dto.PrintedInvoiceRequested);

        Address? billingAddress = null;
        if (customerType == CustomerType.Company)
        {
            if (dto.AddressId.HasValue)
            {
                billingAddress = await _unitOfWork.AddressRepository.GetByIdAsync(
                    dto.AddressId.Value,
                    cancellationToken);
                if (user is null || billingAddress is null || user.BillingAddressId != dto.AddressId)
                {
                    throw new InvalidOperationException("The selected billing address does not belong to the user.");
                }
            }
            else
            {
                ValidateManualAddress(dto.PostalCode, dto.City, dto.Street, dto.HouseNumber, "billing");
            }

            ValidateCompanyBilling(billingCompanyName, billingTaxNumber);
        }
        else
        {
            billingCompanyName = null;
            billingTaxNumber = null;
        }

        Address? deliveryAddress = null;
        if (dto.FulfillmentMethod != FulfillmentMethod.PersonalPickup)
        {
            if (dto.DeliveryAddressId.HasValue)
            {
                deliveryAddress = await _unitOfWork.AddressRepository.GetByIdAsync(
                    dto.DeliveryAddressId.Value,
                    cancellationToken);
                if (user is null || deliveryAddress is null || user.ShippingAddressId != dto.DeliveryAddressId)
                {
                    throw new InvalidOperationException("The selected delivery address does not belong to the user.");
                }
            }
            else
            {
                ValidateManualAddress(
                    dto.DeliveryPostalCode,
                    dto.DeliveryCity,
                    dto.DeliveryStreet,
                    dto.DeliveryHouseNumber,
                    "delivery");
            }
        }

        return new OrderCustomerSnapshot(
            AddressId: customerType == CustomerType.Company ? dto.AddressId : null,
            CustomerName: customerContact.Name,
            CustomerEmail: customerContact.Email,
            CustomerPhone: customerContact.Phone,
            PostalCode: billingAddress?.PostalCode ?? NormalizeOptional(dto.PostalCode) ?? string.Empty,
            City: billingAddress?.City ?? NormalizeOptional(dto.City) ?? string.Empty,
            Street: billingAddress?.Street ?? NormalizeOptional(dto.Street) ?? string.Empty,
            HouseNumber: billingAddress?.HouseNumber ?? NormalizeOptional(dto.HouseNumber) ?? string.Empty,
            Building: billingAddress?.Building ?? NormalizeOptional(dto.Building),
            Floor: billingAddress?.Floor ?? NormalizeOptional(dto.Floor),
            Door: billingAddress?.Door ?? NormalizeOptional(dto.Door),
            Doorbell: billingAddress?.Doorbell ?? NormalizeOptional(dto.Doorbell),
            BillingCompanyName: NormalizeOptional(billingCompanyName),
            BillingTaxNumber: NormalizeOptional(billingTaxNumber),
            CustomerType: customerType,
            PrintedInvoiceRequested: printedInvoiceRequested,
            DeliveryPostalCode: deliveryAddress?.PostalCode ?? NormalizeOptional(dto.DeliveryPostalCode),
            DeliveryCity: deliveryAddress?.City ?? NormalizeOptional(dto.DeliveryCity),
            DeliveryStreet: deliveryAddress?.Street ?? NormalizeOptional(dto.DeliveryStreet),
            DeliveryHouseNumber: deliveryAddress?.HouseNumber ?? NormalizeOptional(dto.DeliveryHouseNumber),
            DeliveryBuilding: deliveryAddress?.Building ?? NormalizeOptional(dto.DeliveryBuilding),
            DeliveryFloor: deliveryAddress?.Floor ?? NormalizeOptional(dto.DeliveryFloor),
            DeliveryDoor: deliveryAddress?.Door ?? NormalizeOptional(dto.DeliveryDoor),
            DeliveryDoorbell: deliveryAddress?.Doorbell ?? NormalizeOptional(dto.DeliveryDoorbell));
    }

    private async Task<Guid?> ResolveGuestOrderUserIdAsync(
        string customerEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByEmailAsync(customerEmail);
        if (user is null ||
            !user.EmailConfirmed ||
            user.RecordStatus != RecordStatus.Active)
        {
            return null;
        }

        return user.Id;
    }

    private static OrderCustomerContact BuildCustomerContact(
        ApplicationUser? user,
        bool useManualContact,
        PlaceOrderDto dto) =>
        user is null || useManualContact
            ? BuildGuestContact(dto)
            : BuildAccountContact(user);

    private static OrderCustomerContact BuildGuestContact(PlaceOrderDto dto)
    {
        var customerName = NormalizeOptional(dto.CustomerName);
        var customerEmail = NormalizeOptional(dto.CustomerEmail);
        var customerPhone = NormalizeOptional(dto.CustomerPhone);
        if (customerName is null || customerEmail is null || customerPhone is null)
        {
            throw new ArgumentException("Guest customer and address details are required.");
        }

        if (!System.Net.Mail.MailAddress.TryCreate(customerEmail, out _))
        {
            throw new ArgumentException("Invalid email address.");
        }

        return new OrderCustomerContact(
            customerName,
            customerEmail,
            customerPhone);
    }

    private static void ValidateAcceptedLegalVersions(PlaceOrderDto dto)
    {
        if (dto.AcceptedTermsVersion != LegalDocumentVersions.CurrentTermsVersion)
        {
            throw new InvalidOperationException("Az aktuális Általános Szerződési Feltételek elfogadása kötelező.");
        }

        if (dto.AcceptedPrivacyPolicyVersion != LegalDocumentVersions.CurrentPrivacyPolicyVersion)
        {
            throw new InvalidOperationException("Az aktuális Adatkezelési Tájékoztató elfogadása kötelező.");
        }
    }

    private static void ValidateManualAddress(string? postalCode, string? city, string? street, string? houseNumber, string kind)
    {
        if (new[] { postalCode, city, street, houseNumber }.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException($"The {kind} address is incomplete.");
    }

    private static void ValidateCompanyBilling(string? companyName, string? taxNumber)
    {
        var normalizedCompanyName = NormalizeOptional(companyName);
        var normalizedTaxNumber = NormalizeOptional(taxNumber);
        if ((normalizedCompanyName is null) != (normalizedTaxNumber is null))
            throw new ArgumentException("Céges számlázáshoz a cégnév és az adószám együttes megadása szükséges.");
        if (normalizedTaxNumber is not null &&
            !System.Text.RegularExpressions.Regex.IsMatch(normalizedTaxNumber, @"^\d{8}(-\d-\d{2})?$"))
            throw new ArgumentException("Az adószám formátuma hibás. Példa: 12345678-1-12.");
    }

    private static OrderCustomerContact BuildAccountContact(ApplicationUser user)
    {
        var firstName = NormalizeOptional(user.FirstName);
        var lastName = NormalizeOptional(user.LastName);
        var email = NormalizeOptional(user.Email);
        var phone = NormalizeOptional(user.PhoneNumber);
        if (firstName is null || lastName is null || email is null || phone is null)
        {
            throw new ArgumentException(
                "A fiók személyes adatai hiányosak. Töltsd ki a nevet, email címet és telefonszámot, vagy válaszd a Más adatok megadása lehetőséget.");
        }

        return new OrderCustomerContact(
            $"{lastName} {firstName}".Trim(),
            email,
            phone);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeIdempotencyKey(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            throw new ArgumentException("A rendelés azonosító kulcsa hiányzik. Kérjük, frissítsd az oldalt, és próbáld újra.");
        }

        if (normalized.Length > 100 ||
            !System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[A-Za-z0-9_-]{8,100}$"))
        {
            throw new ArgumentException("A rendelés azonosító kulcsa érvénytelen. Kérjük, frissítsd az oldalt, és próbáld újra.");
        }

        return normalized;
    }

    private static string NormalizeRequired(string? value, string fieldName, int minLength, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < minLength)
        {
            throw new ArgumentException($"{fieldName} legalább {minLength} karakter legyen.");
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} legfeljebb {maxLength} karakter lehet.");
        }

        return normalized;
    }

    private void EnsureSumUpCheckoutMatchesOrder(Order order, decimal? amount, string? currency)
    {
        if (!amount.HasValue)
        {
            throw new InvalidOperationException("SumUp checkout amount is missing.");
        }

        if (amount.Value != order.TotalPrice)
        {
            throw new InvalidOperationException("SumUp checkout amount does not match the order total.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new InvalidOperationException("SumUp checkout currency is missing.");
        }

        if (!string.Equals(currency.Trim(), _sumUpOptions.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SumUp checkout currency does not match the configured currency.");
        }
    }

    private DateTime GetUtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private string GenerateOrderNumber()
    {
        return $"BK-{GetUtcNow():yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }

    private async Task<Order?> GetLatestCheckedOutOrderAsync(
        Guid? userId,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        var checkedOutCart = userId.HasValue
            ? await _unitOfWork.CartRepository.GetLatestCheckedOutByUserIdAsync(userId.Value, cancellationToken)
            : string.IsNullOrWhiteSpace(sessionId)
                ? null
                : await _unitOfWork.CartRepository.GetLatestCheckedOutBySessionIdAsync(sessionId, cancellationToken);

        return checkedOutCart is null
            ? null
            : await _unitOfWork.OrderRepository.GetByCartIdWithItemsAsync(
                checkedOutCart.Id,
                cancellationToken);
    }

    private async Task<Order> ReloadOrderWithItemsAsync(
        Guid orderId,
        CancellationToken cancellationToken) =>
        await _unitOfWork.OrderRepository.GetByIdWithItemsAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("Order reload failed after saving changes.");

    private async Task SendOrderCreatedEmailsAsync(
        OrderDto order,
        CancellationToken cancellationToken)
    {
        var customerMessage = _emailTemplateService.BuildOrderConfirmationMessage(
            order.CustomerEmail,
            order);
        await _emailService.SendAsync(customerMessage, cancellationToken);

        var businessMessage = _emailTemplateService.BuildOrderBusinessNotificationMessage(
            _emailBrandingOptions.ContactEmail,
            order);
        await _emailService.SendAsync(businessMessage, cancellationToken);
    }

    private sealed record OrderCustomerSnapshot(
        int? AddressId,
        string CustomerName,
        string CustomerEmail,
        string CustomerPhone,
        string PostalCode,
        string City,
        string Street,
        string HouseNumber,
        string? Building,
        string? Floor,
        string? Door,
        string? Doorbell,
        string? BillingCompanyName,
        string? BillingTaxNumber,
        CustomerType CustomerType,
        bool PrintedInvoiceRequested,
        string? DeliveryPostalCode,
        string? DeliveryCity,
        string? DeliveryStreet,
        string? DeliveryHouseNumber,
        string? DeliveryBuilding,
        string? DeliveryFloor,
        string? DeliveryDoor,
        string? DeliveryDoorbell);

    private sealed record OrderCustomerContact(
        string Name,
        string Email,
        string Phone);
}
