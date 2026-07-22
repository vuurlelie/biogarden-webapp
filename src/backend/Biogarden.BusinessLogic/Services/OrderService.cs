using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.DTOs.Payments;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
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
    private readonly EmailBrandingOptions _emailBrandingOptions;

    public OrderService(
        IUnitOfWork unitOfWork,
        ICartService cartService,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        ISumUpCheckoutService sumUpCheckoutService,
        IOptions<EmailBrandingOptions> emailBrandingOptions)
    {
        _unitOfWork = unitOfWork;
        _cartService = cartService;
        _userManager = userManager;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _sumUpCheckoutService = sumUpCheckoutService;
        _emailBrandingOptions = emailBrandingOptions.Value;
    }

    public async Task<OrderDto> CreateOrderFromCartAsync(
        Guid? userId,
        string? sessionId,
        PlaceOrderDto dto,
        CancellationToken cancellationToken = default)
    {
        var cart = userId.HasValue
            ? await _unitOfWork.CartRepository.GetActiveByUserIdAsync(userId.Value, cancellationToken)
            : string.IsNullOrWhiteSpace(sessionId)
                ? null
                : await _unitOfWork.CartRepository.GetActiveBySessionIdAsync(sessionId, cancellationToken);

        if (cart is null)
        {
            throw new InvalidOperationException("Cart not found.");
        }

        if (cart.IsCheckedOut)
        {
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
            cart.Items.Any(item => !item.Product.DeliveryAvailable))
        {
            throw new InvalidOperationException(
                "The cart contains a product that is available only for personal pickup.");
        }

        var validationResult = await _cartService.ValidateCartForCheckoutAsync(
            userId,
            sessionId,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException("Cart validation failed.");
        }

        ApplicationUser? user = userId.HasValue
            ? await _userManager.FindByIdAsync(userId.Value.ToString())
            : null;
        if (userId.HasValue && user is null) throw new InvalidOperationException("User not found.");
        var useManualContact = !string.IsNullOrWhiteSpace(dto.CustomerName) ||
            !string.IsNullOrWhiteSpace(dto.CustomerEmail) ||
            !string.IsNullOrWhiteSpace(dto.CustomerPhone);
        if (user is null || useManualContact) ValidateGuestContactDetails(dto); else ValidateAccountContactDetails(user);

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
                billingAddress = await _unitOfWork.AddressRepository.GetByIdAsync(dto.AddressId.Value, cancellationToken);
                if (user is null || billingAddress is null || user.BillingAddressId != dto.AddressId)
                    throw new InvalidOperationException("The selected billing address does not belong to the user.");
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
                deliveryAddress = await _unitOfWork.AddressRepository.GetByIdAsync(dto.DeliveryAddressId.Value, cancellationToken);
                if (user is null || deliveryAddress is null || user.ShippingAddressId != dto.DeliveryAddressId)
                    throw new InvalidOperationException("The selected delivery address does not belong to the user.");
            }
            else
            {
                ValidateManualAddress(dto.DeliveryPostalCode, dto.DeliveryCity, dto.DeliveryStreet, dto.DeliveryHouseNumber, "delivery");
            }
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = GenerateOrderNumber(),
            UserId = userId,
            AddressId = customerType == CustomerType.Company ? dto.AddressId : null,
            CustomerName = user is null || useManualContact
                ? dto.CustomerName!.Trim()
                : $"{user.LastName} {user.FirstName}".Trim(),
            CustomerEmail = user is null || useManualContact ? dto.CustomerEmail!.Trim() : user.Email!.Trim(),
            CustomerPhone = user is null || useManualContact ? dto.CustomerPhone!.Trim() : user.PhoneNumber!.Trim(),
            PostalCode = billingAddress?.PostalCode ?? NormalizeOptional(dto.PostalCode) ?? string.Empty,
            City = billingAddress?.City ?? NormalizeOptional(dto.City) ?? string.Empty,
            Street = billingAddress?.Street ?? NormalizeOptional(dto.Street) ?? string.Empty,
            HouseNumber = billingAddress?.HouseNumber ?? NormalizeOptional(dto.HouseNumber) ?? string.Empty,
            Building = billingAddress?.Building ?? NormalizeOptional(dto.Building),
            Floor = billingAddress?.Floor ?? NormalizeOptional(dto.Floor),
            Door = billingAddress?.Door ?? NormalizeOptional(dto.Door),
            Doorbell = billingAddress?.Doorbell ?? NormalizeOptional(dto.Doorbell),
            BillingCompanyName = NormalizeOptional(billingCompanyName),
            BillingTaxNumber = NormalizeOptional(billingTaxNumber),
            CustomerType = customerType,
            PrintedInvoiceRequested = printedInvoiceRequested,
            DeliveryPostalCode = deliveryAddress?.PostalCode ?? NormalizeOptional(dto.DeliveryPostalCode),
            DeliveryCity = deliveryAddress?.City ?? NormalizeOptional(dto.DeliveryCity),
            DeliveryStreet = deliveryAddress?.Street ?? NormalizeOptional(dto.DeliveryStreet),
            DeliveryHouseNumber = deliveryAddress?.HouseNumber ?? NormalizeOptional(dto.DeliveryHouseNumber),
            DeliveryBuilding = deliveryAddress?.Building ?? NormalizeOptional(dto.DeliveryBuilding),
            DeliveryFloor = deliveryAddress?.Floor ?? NormalizeOptional(dto.DeliveryFloor),
            DeliveryDoor = deliveryAddress?.Door ?? NormalizeOptional(dto.DeliveryDoor),
            DeliveryDoorbell = deliveryAddress?.Doorbell ?? NormalizeOptional(dto.DeliveryDoorbell),
            CartId = cart.Id,
            CreatedAtUtc = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            FulfillmentMethod = dto.FulfillmentMethod
        };

        decimal totalPrice = 0m;

        foreach (var cartItem in cart.Items)
        {
            var product = await _unitOfWork.ProductRepository.GetByIdAsync(
                cartItem.ProductId,
                cancellationToken);

            if (product is null)
            {
                throw new InvalidOperationException("Product missing during order creation.");
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
            if (product.StockQuantity < 0)
                throw new InvalidOperationException("Stock inconsistency detected during order creation.");
        }

        order.TotalPrice = totalPrice;
        order.StockCommitted = true;

        cart.IsCheckedOut = true;
        cart.CheckedOutAtUtc = DateTime.UtcNow;

        await _unitOfWork.OrderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var createdOrder = await _unitOfWork.OrderRepository.GetByIdWithItemsAsync(
            order.Id,
            cancellationToken);

        var createdOrderDto = createdOrder!.ToDto();
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
            foreach (var orderItem in order.Items)
            {
                var product = await _unitOfWork.ProductRepository.GetByIdAsync(orderItem.ProductId, cancellationToken);
                if (product is null) throw new InvalidOperationException("Product missing during payment confirmation.");

                product.StockQuantity -= orderItem.Quantity;
                if (product.StockQuantity < 0) throw new InvalidOperationException("Stock inconsistency detected.");
            }
            order.StockCommitted = true;
        }

        order.PaymentStatus = PaymentStatus.Paid;
        order.Status = OrderStatus.Confirmed;
        order.ConfirmedAtUtc = DateTime.UtcNow;

        _unitOfWork.OrderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var confirmedOrder = await _unitOfWork.OrderRepository.GetByIdWithItemsAsync(
            order.Id,
            cancellationToken);

        return confirmedOrder!.ToDto();
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

        EnsureCanAccessOrderPayment(order, userId, sessionId);

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

        if (order.PaymentStatus != PaymentStatus.Paid)
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.Status = OrderStatus.Confirmed;
            order.ConfirmedAtUtc ??= DateTime.UtcNow;
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

        order.Status = OrderStatus.Cancelled;
        order.CancelledAtUtc = DateTime.UtcNow;
        order.DeletedAtUtc = order.CancelledAtUtc;
        order.CancellationReason = string.IsNullOrWhiteSpace(dto.Reason)
            ? null
            : dto.Reason.Trim();

        _unitOfWork.OrderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var cancelledOrder = await _unitOfWork.OrderRepository.GetByIdWithItemsAsync(
            order.Id,
            cancellationToken);

        return cancelledOrder!.ToDto();
    }

    public async Task<List<OrderDto>> GetMyOrdersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var orders = await _unitOfWork.OrderRepository.GetByUserIdAsync(
            userId,
            cancellationToken);

        var visibleFromUtc = DateTime.UtcNow.AddDays(-7);

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
            !IsVisibleInUserHistory(order, DateTime.UtcNow.AddDays(-7)))
        {
            throw new KeyNotFoundException("Order not found.");
        }

        return order.ToDto();
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

    private static void EnsureCanAccessOrderPayment(Order order, Guid? userId, string? sessionId)
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

    private static void ValidateGuestContactDetails(PlaceOrderDto dto)
    {
        var required = new[]
        {
            dto.CustomerName, dto.CustomerEmail, dto.CustomerPhone
        };
        if (required.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Guest customer and address details are required.");
        }

        if (!System.Net.Mail.MailAddress.TryCreate(dto.CustomerEmail, out _))
        {
            throw new ArgumentException("Invalid email address.");
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

    private static void ValidateAccountContactDetails(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.FirstName) ||
            string.IsNullOrWhiteSpace(user.LastName) ||
            string.IsNullOrWhiteSpace(user.Email) ||
            string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            throw new ArgumentException(
                "A fiók személyes adatai hiányosak. Töltsd ki a nevet, email címet és telefonszámot, vagy válaszd a Más adatok megadása lehetőséget.");
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GenerateOrderNumber()
    {
        return $"BK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }

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
}
