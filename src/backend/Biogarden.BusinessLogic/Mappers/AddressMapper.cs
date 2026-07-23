using Biogarden.BusinessLogic.DTOs.Addresses;
using Biogarden.DataAccess.Entities;

namespace Biogarden.BusinessLogic.Mappers;

public static class AddressMapper
{
    public static AddressListItemDto ToListItemDto(this Address address)
    {
        return new AddressListItemDto
        {
            Id = address.Id,
            PostalCode = address.PostalCode,
            City = address.City,
            Street = address.Street,
            HouseNumber = address.HouseNumber,
            FullAddress = address.ToDisplayAddress()
        };
    }

    public static List<AddressListItemDto> ToListItemDtos(
        this IEnumerable<Address> addresses)
    {
        return addresses
            .Select(address => address.ToListItemDto())
            .ToList();
    }

    public static AddressDetailsDto ToDetailsDto(this Address address)
    {
        return new AddressDetailsDto
        {
            Id = address.Id,
            PostalCode = address.PostalCode,
            City = address.City,
            Street = address.Street,
            HouseNumber = address.HouseNumber,
            Building = address.Building,
            Floor = address.Floor,
            Door = address.Door,
            Doorbell = address.Doorbell,
            FullAddress = address.ToDisplayAddress()
        };
    }

    public static Address ToEntity(this AddressCreateDto addressCreateDto)
    {
        return new Address
        {
            PostalCode = addressCreateDto.PostalCode.Trim(),
            City = addressCreateDto.City.Trim(),
            Street = addressCreateDto.Street.Trim(),
            HouseNumber = addressCreateDto.HouseNumber.Trim(),
            Building = NormalizeOptional(addressCreateDto.Building),
            Floor = NormalizeOptional(addressCreateDto.Floor),
            Door = NormalizeOptional(addressCreateDto.Door),
            Doorbell = NormalizeOptional(addressCreateDto.Doorbell)
        };
    }

    public static void ApplyChanges(
        this Address address,
        AddressEditDto addressEditDto)
    {
        address.PostalCode = addressEditDto.PostalCode.Trim();
        address.City = addressEditDto.City.Trim();
        address.Street = addressEditDto.Street.Trim();
        address.HouseNumber = addressEditDto.HouseNumber.Trim();
        address.Building = NormalizeOptional(addressEditDto.Building);
        address.Floor = NormalizeOptional(addressEditDto.Floor);
        address.Door = NormalizeOptional(addressEditDto.Door);
        address.Doorbell = NormalizeOptional(addressEditDto.Doorbell);
    }

    public static string ToDisplayAddress(this Address address)
    {
        var addressParts = new List<string>
        {
            $"{address.PostalCode} {address.City}",
            $"{address.Street} {address.HouseNumber}"
        };

        var optionalParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(address.Building))
        {
            optionalParts.Add($"{address.Building}. épület");
        }

        if (!string.IsNullOrWhiteSpace(address.Floor))
        {
            optionalParts.Add($"{address.Floor}. emelet");
        }

        if (!string.IsNullOrWhiteSpace(address.Door))
        {
            optionalParts.Add($"{address.Door}. ajtó");
        }

        if (!string.IsNullOrWhiteSpace(address.Doorbell))
        {
            optionalParts.Add($"csengő: {address.Doorbell}");
        }

        if (optionalParts.Count > 0)
        {
            addressParts.Add(string.Join(", ", optionalParts));
        }

        return string.Join(", ", addressParts);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
