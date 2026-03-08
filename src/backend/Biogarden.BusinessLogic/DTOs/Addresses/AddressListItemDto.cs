namespace Biogarden.BusinessLogic.DTOs.Addresses;

public class AddressListItemDto
{
    public int Id { get; set; }

    public string PostalCode { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Street { get; set; } = string.Empty;

    public string HouseNumber { get; set; } = string.Empty;

    public string FullAddress { get; set; } = string.Empty;
}