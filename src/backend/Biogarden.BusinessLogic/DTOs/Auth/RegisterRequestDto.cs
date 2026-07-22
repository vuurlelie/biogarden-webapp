using Biogarden.BusinessLogic.DTOs.Addresses;
using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Auth;

public class RegisterRequestDto
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;

    public CustomerType CustomerType { get; set; } = CustomerType.Private;

    public string? CompanyName { get; set; }

    public string? TaxNumber { get; set; }

    public AddressCreateDto? BillingAddress { get; set; }

    public bool PrintedInvoiceRequested { get; set; }
}
