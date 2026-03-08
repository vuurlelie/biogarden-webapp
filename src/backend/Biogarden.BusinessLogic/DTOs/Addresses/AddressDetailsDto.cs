namespace Biogarden.BusinessLogic.DTOs.Addresses;

public class AddressDetailsDto
{
    public int Id { get; set; }

    public string PostalCode { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Street { get; set; } = string.Empty;

    public string HouseNumber { get; set; } = string.Empty;

    public string? Building { get; set; }

    public string? Floor { get; set; }

    public string? Door { get; set; }

    public string? Doorbell { get; set; }

    public string FullAddress { get; set; } = string.Empty;
}