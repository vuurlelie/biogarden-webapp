using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;

namespace Biogarden.BusinessLogic.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartService _cartService;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrderService(
        IUnitOfWork unitOfWork,
        ICartService cartService,
        UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _cartService = cartService;
        _userManager = userManager;
    }

    public async Task<OrderDto> CreateOrderFromCartAsync(
        Guid userId,
        string? sessionId,
        PlaceOrderDto dto,
        CancellationToken cancellationToken = default)
    {
        var cart = await _unitOfWork.CartRepository.GetActiveByUserIdAsync(
            userId,
            cancellationToken);

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

        var validationResult = await _cartService.ValidateCartForCheckoutAsync(
            userId,
            sessionId,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException("Cart validation failed.");
        }

        var address = await _unitOfWork.AddressRepository.GetByIdAsync(
            dto.AddressId,
            cancellationToken);

        if (address is null)
        {
            throw new InvalidOperationException("Invalid address.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var isUsersAddress =
            user.ShippingAddressId == dto.AddressId ||
            user.BillingAddressId == dto.AddressId;

        if (!isUsersAddress)
        {
            throw new InvalidOperationException(
                "The selected address does not belong to the user.");
        }

        var existingOrders = await _unitOfWork.OrderRepository.GetByUserIdAsync(
            userId,
            cancellationToken);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = GenerateOrderNumber(existingOrders.Count + 1),
            UserId = userId,
            AddressId = dto.AddressId,
            CartId = cart.Id,
            CreatedAtUtc = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending
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
                Quantity = cartItem.Quantity,
                LineTotal = lineTotal
            });

            totalPrice += lineTotal;
        }

        order.TotalPrice = totalPrice;

        cart.IsCheckedOut = true;
        cart.CheckedOutAtUtc = DateTime.UtcNow;

        await _unitOfWork.OrderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var createdOrder = await _unitOfWork.OrderRepository.GetByIdWithItemsAsync(
            order.Id,
            cancellationToken);

        return createdOrder!.ToDto();
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

        if (order.PaymentStatus == PaymentStatus.Paid ||
            order.PaymentStatus == PaymentStatus.RefundPending ||
            order.PaymentStatus == PaymentStatus.Refunded)
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
        }

        order.Status = OrderStatus.Cancelled;
        order.CancelledAtUtc = DateTime.UtcNow;
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

        return orders.Select(order => order.ToDto()).ToList();
    }

    public async Task<OrderDto> GetOrderByIdAsync(
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

        return order.ToDto();
    }

    private static string GenerateOrderNumber(int sequence)
    {
        return $"BG-{DateTime.UtcNow:yyyyMMdd}-{sequence:D4}";
    }
}
