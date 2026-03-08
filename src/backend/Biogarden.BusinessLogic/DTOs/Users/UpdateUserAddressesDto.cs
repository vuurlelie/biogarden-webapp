namespace Biogarden.BusinessLogic.DTOs.Users;

public class UpdateUserAddressesDto
{
    public int? ShippingAddressId { get; set; }

    public int? BillingAddressId { get; set; }
}