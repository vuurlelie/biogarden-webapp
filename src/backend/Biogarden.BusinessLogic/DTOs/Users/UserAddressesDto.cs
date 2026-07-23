using Biogarden.BusinessLogic.DTOs.Addresses;

namespace Biogarden.BusinessLogic.DTOs.Users;

public class UserAddressesDto
{
    public AddressDetailsDto? ShippingAddress { get; set; }

    public AddressDetailsDto? BillingAddress { get; set; }
}
