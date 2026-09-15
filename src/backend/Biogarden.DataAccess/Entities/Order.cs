using Biogarden.DataAccess.Enums;

namespace Biogarden.DataAccess.Entities;

public class Order
{
    public Guid Id { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public required string OrderNumber { get; set; }

    public string? IdempotencyKey { get; set; }

    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public int? AddressId { get; set; }
    public Address? Address { get; set; }

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
    public CustomerType CustomerType { get; set; } = CustomerType.Private;
    public bool PrintedInvoiceRequested { get; set; }
    public string AcceptedTermsVersion { get; set; } = string.Empty;
    public string AcceptedPrivacyPolicyVersion { get; set; } = string.Empty;

    public string? DeliveryPostalCode { get; set; }
    public string? DeliveryCity { get; set; }
    public string? DeliveryStreet { get; set; }
    public string? DeliveryHouseNumber { get; set; }
    public string? DeliveryBuilding { get; set; }
    public string? DeliveryFloor { get; set; }
    public string? DeliveryDoor { get; set; }
    public string? DeliveryDoorbell { get; set; }

    public Guid CartId { get; set; }
    public Cart? Cart { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public decimal TotalPrice { get; set; }

    public bool StockCommitted { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public FulfillmentMethod FulfillmentMethod { get; set; } = FulfillmentMethod.PersonalPickup;

    public string? SumUpCheckoutId { get; set; }
    public string? SumUpTransactionId { get; set; }

    public string? CancellationReason { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderWithdrawalRequest> WithdrawalRequests { get; set; } = new List<OrderWithdrawalRequest>();
}
