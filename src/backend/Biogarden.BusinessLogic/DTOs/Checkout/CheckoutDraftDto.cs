using System.Text.Json.Serialization;

namespace Biogarden.BusinessLogic.DTOs.Checkout;

// Incomplete fields are valid draft content; order submission uses its own validation.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CheckoutDraftFields
{
    public Biogarden.DataAccess.Enums.FulfillmentMethod? FulfillmentMethod { get; set; }
    public string? GlsDeliveryPointId { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string Street { get; set; } = "";
    public string HouseNumber { get; set; } = "";
    public string Building { get; set; } = "";
    public string Floor { get; set; } = "";
    public string Door { get; set; } = "";
    public string Doorbell { get; set; } = "";
    public string BillingCompanyName { get; set; } = "";
    public string BillingTaxNumber { get; set; } = "";
    public string DeliveryPostalCode { get; set; } = "";
    public string DeliveryCity { get; set; } = "";
    public string DeliveryStreet { get; set; } = "";
    public string DeliveryHouseNumber { get; set; } = "";
    public string DeliveryBuilding { get; set; } = "";
    public string DeliveryFloor { get; set; } = "";
    public string DeliveryDoor { get; set; } = "";
    public string DeliveryDoorbell { get; set; } = "";
    public int CustomerType { get; set; }
    public bool PrintedInvoiceRequested { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveCheckoutDraftDto(Guid? Version, CheckoutDraftFields Fields);
public sealed record CheckoutDraftDto(Guid? Version, DateTime? ExpiresAtUtc, CheckoutDraftFields? Fields,
    Biogarden.BusinessLogic.DTOs.Orders.GlsDeliveryPointDto? DeliveryPoint = null, string? DeliveryPointError = null);
