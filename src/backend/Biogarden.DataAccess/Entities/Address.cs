using Biogarden.DataAccess.Enums;

namespace Biogarden.DataAccess.Entities;

public class Address
{
    public int Id { get; set; }

    public required string PostalCode { get; set; }

    public required string City { get; set; }

    public required string Street { get; set; }

    public required string HouseNumber { get; set; }

    public string? Building { get; set; }

    public string? Floor { get; set; }

    public string? Door { get; set; }

    public string? Doorbell { get; set; }

    public RecordStatus RecordStatus { get; set; } = RecordStatus.Active;
}