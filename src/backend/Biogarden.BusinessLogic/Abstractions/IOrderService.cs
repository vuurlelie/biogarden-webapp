using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.DTOs.Payments;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IOrderService
{
    Task<OrderDto> CreateOrderFromCartAsync(
        Guid? userId,
        string? sessionId,
        PlaceOrderDto dto,
        CancellationToken cancellationToken = default);

    Task<OrderDto> ConfirmOrderPaymentAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<StripeCheckoutSessionDto> CreateStripeCheckoutSessionAsync(
        Guid? userId,
        string? sessionId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<OrderDto> ConfirmStripeCheckoutSessionAsync(
        string stripeCheckoutSessionId,
        string? stripePaymentIntentId,
        CancellationToken cancellationToken = default);

    Task<OrderDto?> MarkStripePaymentFailedAsync(
        string stripeCheckoutSessionId,
        string? stripePaymentIntentId,
        CancellationToken cancellationToken = default);

    Task<OrderDto> CancelOrderAsync(
        Guid userId,
        Guid orderId,
        CancelOrderDto dto,
        CancellationToken cancellationToken = default);

    Task<List<OrderDto>> GetMyOrdersAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<OrderDto> GetOrderByIdAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken = default);
}
