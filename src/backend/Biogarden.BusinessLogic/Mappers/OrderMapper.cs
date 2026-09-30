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
            IdempotencyKey = order.IdempotencyKey,
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
            CustomerType = order.CustomerType,
            PrintedInvoiceRequested = order.PrintedInvoiceRequested,
            AcceptedTermsVersion = order.AcceptedTermsVersion,
            AcceptedPrivacyPolicyVersion = order.AcceptedPrivacyPolicyVersion,
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
            Subtotal = order.Subtotal,
            DeliveryFee = order.DeliveryFee,
            PaymentFee = order.PaymentFee,
            Currency = order.Currency,
            PaymentMethod = order.PaymentMethod,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            FulfillmentMethod = order.FulfillmentMethod,
            SumUpCheckoutId = order.SumUpCheckoutId,
            SumUpTransactionId = order.SumUpTransactionId,
            CancellationReason = order.CancellationReason,
            Items = order.Items.Select(item => new OrderItemDto
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                UnitPrice = item.UnitPrice,
                Unit = item.Unit,
                Quantity = item.Quantity,
                LineTotal = item.LineTotal
            }).ToList(),
            WithdrawalRequests = order.WithdrawalRequests
                .OrderByDescending(request => request.RequestedAtUtc)
                .Select(request => new OrderWithdrawalRequestDto
                {
                    Id = request.Id,
                    OrderId = request.OrderId,
                    CustomerName = request.CustomerName,
                    CustomerEmail = request.CustomerEmail,
                    Reason = request.Reason,
                    Status = request.Status,
                    RequestedAtUtc = request.RequestedAtUtc,
                    HandledAtUtc = request.HandledAtUtc
                })
                .ToList()
        };
    }
}
