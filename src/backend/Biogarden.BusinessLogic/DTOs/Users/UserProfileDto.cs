using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Users;

public class UserProfileDto
{
    public string Email { get; set; } = string.Empty;

    public CustomerType CustomerType { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? CompanyName { get; set; }

    public string? TaxNumber { get; set; }

    public bool PrintedInvoiceRequested { get; set; }
}
