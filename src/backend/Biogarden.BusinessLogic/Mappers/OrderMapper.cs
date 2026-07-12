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
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            PostalCode = order.PostalCode,
            City = order.City,
            Street = order.Street,
            HouseNumber = order.HouseNumber,
            Building = order.Building,
            Floor = order.Floor,
            Door = order.Door,
            Doorbell = order.Doorbell,
            BillingCompanyName = order.BillingCompanyName,
            BillingTaxNumber = order.BillingTaxNumber,
            DeliveryPostalCode = order.DeliveryPostalCode,
            DeliveryCity = order.DeliveryCity,
            DeliveryStreet = order.DeliveryStreet,
            DeliveryHouseNumber = order.DeliveryHouseNumber,
            DeliveryBuilding = order.DeliveryBuilding,
            DeliveryFloor = order.DeliveryFloor,
            DeliveryDoor = order.DeliveryDoor,
            DeliveryDoorbell = order.DeliveryDoorbell,
            CreatedAtUtc = order.CreatedAtUtc,
            ConfirmedAtUtc = order.ConfirmedAtUtc,
            CancelledAtUtc = order.CancelledAtUtc,
            DeletedAtUtc = order.DeletedAtUtc,
            TotalPrice = order.TotalPrice,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            FulfillmentMethod = order.FulfillmentMethod,
            StripeCheckoutSessionId = order.StripeCheckoutSessionId,
            StripePaymentIntentId = order.StripePaymentIntentId,
            CancellationReason = order.CancellationReason,
            Items = order.Items.Select(item => new OrderItemDto
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                UnitPrice = item.UnitPrice,
                Unit = item.Unit,
                Quantity = item.Quantity,
                LineTotal = item.LineTotal
            }).ToList()
        };
    }
}
