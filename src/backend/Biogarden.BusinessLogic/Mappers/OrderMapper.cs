using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.DataAccess.Entities;

namespace Biogarden.BusinessLogic.Mappers;

public static class OrderMapper
{
    public static OrderDto ToDto(this Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            UserId = order.UserId,
            AddressId = order.AddressId,
            CreatedAtUtc = order.CreatedAtUtc,
            ConfirmedAtUtc = order.ConfirmedAtUtc,
            CancelledAtUtc = order.CancelledAtUtc,
            TotalPrice = order.TotalPrice,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            FulfillmentMethod = order.FulfillmentMethod,
            CancellationReason = order.CancellationReason,
            Items = order.Items.Select(item => new OrderItemDto
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                LineTotal = item.LineTotal
            }).ToList()
        };
    }
}
