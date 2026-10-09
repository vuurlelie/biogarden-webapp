using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Orders;

public sealed record CheckoutQuoteRequest(FulfillmentMethod FulfillmentMethod, OrderPaymentMethod PaymentMethod, string? GlsDeliveryPointId = null);
public sealed record CheckoutMethodDto(FulfillmentMethod FulfillmentMethod, decimal DeliveryFee, OrderPaymentMethod[] PaymentMethods);
public sealed record CheckoutQuoteDto(
    Guid CartId,
    FulfillmentMethod FulfillmentMethod,
    OrderPaymentMethod PaymentMethod,
    string Currency,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal PaymentFee,
    decimal TotalPrice,
    string Version,
    IReadOnlyList<OrderItemDto> Items,
    GlsDeliveryPointDto? DeliveryPoint = null
);
