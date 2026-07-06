using Biogarden.BusinessLogic.DTOs.Addresses;

namespace Biogarden.BusinessLogic.DTOs.Users;

public class UpdateUserAddressesDto
{
    public AddressCreateDto? ShippingAddress { get; set; }

    public AddressCreateDto? BillingAddress { get; set; }

    public bool UseShippingAsBilling { get; set; }
}
