using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Addresses;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.DataAccess.Abstractions;

namespace Biogarden.BusinessLogic.Services;

public class AddressService : IAddressService
{
    private readonly IUnitOfWork _unitOfWork;

    public AddressService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<AddressListItemDto>> GetAllAsync()
    {
        var addresses = await _unitOfWork.Addresses.GetAllAsync();

        return addresses.ToListItemDtos();
    }

    public async Task<AddressDetailsDto?> GetDetailsAsync(int addressId)
    {
        var address = await _unitOfWork.Addresses.GetByIdAsync(addressId);

        return address?.ToDetailsDto();
    }

    public async Task<int> CreateAsync(AddressCreateDto addressCreateDto)
    {
        var address = addressCreateDto.ToEntity();

        await _unitOfWork.Addresses.AddAsync(address);
        await _unitOfWork.SaveChangesAsync();

        return address.Id;
    }

    public async Task UpdateAsync(int addressId, AddressEditDto addressEditDto)
    {
        var address = await _unitOfWork.Addresses.GetByIdAsync(addressId);

        if (address is null)
        {
            throw new KeyNotFoundException(
                $"Address with id {addressId} was not found.");
        }

        address.ApplyChanges(addressEditDto);

        _unitOfWork.Addresses.Update(address);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteAsync(int addressId)
    {
        var address = await _unitOfWork.Addresses.GetByIdAsync(addressId);

        if (address is null)
        {
            throw new KeyNotFoundException(
                $"Address with id {addressId} was not found.");
        }

        _unitOfWork.Addresses.Delete(address);
        await _unitOfWork.SaveChangesAsync();
    }
}