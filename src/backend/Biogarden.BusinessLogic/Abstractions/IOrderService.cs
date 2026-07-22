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

    Task<SumUpCheckoutDto> CreateSumUpCheckoutAsync(
        Guid? userId,
        string? sessionId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<OrderDto> ConfirmSumUpCheckoutAsync(
        string sumUpCheckoutId,
        string? sumUpTransactionId,
        decimal? amount = null,
        string? currency = null,
        CancellationToken cancellationToken = default);

    Task<OrderDto?> MarkSumUpPaymentFailedAsync(
        string sumUpCheckoutId,
        string? sumUpTransactionId,
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
