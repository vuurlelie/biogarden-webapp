using Biogarden.BusinessLogic.DTOs.Addresses;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IAddressService
{
    Task<List<AddressListItemDto>> GetAllAsync();

    Task<AddressDetailsDto?> GetDetailsAsync(int addressId);

    Task<int> CreateAsync(AddressCreateDto addressCreateDto);

    Task UpdateAsync(int addressId, AddressEditDto addressEditDto);

    Task DeleteAsync(int addressId);
}
