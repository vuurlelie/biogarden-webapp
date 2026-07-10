using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;

namespace Biogarden.DataAccess.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? CompanyName { get; set; }

    public string? TaxNumber { get; set; }

    public int? ShippingAddressId { get; set; }

    public Address? ShippingAddress { get; set; }

    public int? BillingAddressId { get; set; }

    public Address? BillingAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public RecordStatus RecordStatus { get; set; } = RecordStatus.Active;

    public DateTime? DeactivatedAtUtc { get; set; }

    public DateTime? AnonymizedAtUtc { get; set; }
}
