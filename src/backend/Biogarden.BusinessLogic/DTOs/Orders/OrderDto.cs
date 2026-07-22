using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Orders;

public class OrderDto
{
    public Guid Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public int? AddressId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string HouseNumber { get; set; } = string.Empty;
    public string? Building { get; set; }
    public string? Floor { get; set; }
    public string? Door { get; set; }
    public string? Doorbell { get; set; }
    public string? BillingCompanyName { get; set; }
    public string? BillingTaxNumber { get; set; }
    public CustomerType CustomerType { get; set; }
    public bool PrintedInvoiceRequested { get; set; }
    public string? DeliveryPostalCode { get; set; }
    public string? DeliveryCity { get; set; }
    public string? DeliveryStreet { get; set; }
    public string? DeliveryHouseNumber { get; set; }
    public string? DeliveryBuilding { get; set; }
    public string? DeliveryFloor { get; set; }
    public string? DeliveryDoor { get; set; }
    public string? DeliveryDoorbell { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public decimal TotalPrice { get; set; }

    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }

    public FulfillmentMethod FulfillmentMethod { get; set; }

    public string? SumUpCheckoutId { get; set; }
    public string? SumUpTransactionId { get; set; }

    public string? CancellationReason { get; set; }

    public List<OrderItemDto> Items { get; set; } = [];
}
