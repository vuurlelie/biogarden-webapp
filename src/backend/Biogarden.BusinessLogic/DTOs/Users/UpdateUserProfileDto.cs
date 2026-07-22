using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Users;

public class UpdateUserProfileDto
{
    public CustomerType CustomerType { get; set; } = CustomerType.Private;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? CompanyName { get; set; }

    public string? TaxNumber { get; set; }

    public bool PrintedInvoiceRequested { get; set; }
}
