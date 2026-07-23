using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Orders;

public class PlaceOrderDto
{
    public string? IdempotencyKey { get; set; }

    public int? AddressId { get; set; }
    public int? DeliveryAddressId { get; set; }

    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Street { get; set; }
    public string? HouseNumber { get; set; }
    public string? Building { get; set; }
    public string? Floor { get; set; }
    public string? Door { get; set; }
    public string? Doorbell { get; set; }
    public string? BillingCompanyName { get; set; }
    public string? BillingTaxNumber { get; set; }
    public CustomerType CustomerType { get; set; } = CustomerType.Private;
    public bool PrintedInvoiceRequested { get; set; }
    public string? AcceptedTermsVersion { get; set; }
    public string? AcceptedPrivacyPolicyVersion { get; set; }
    public string? DeliveryPostalCode { get; set; }
    public string? DeliveryCity { get; set; }
    public string? DeliveryStreet { get; set; }
    public string? DeliveryHouseNumber { get; set; }
    public string? DeliveryBuilding { get; set; }
    public string? DeliveryFloor { get; set; }
    public string? DeliveryDoor { get; set; }
    public string? DeliveryDoorbell { get; set; }

    public FulfillmentMethod FulfillmentMethod { get; set; }
}
