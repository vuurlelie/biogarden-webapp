using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Addresses;
using Biogarden.BusinessLogic.DTOs.Users;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.BusinessLogic.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return null;
        }

        return new UserProfileDto
        {
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            CompanyName = user.CompanyName,
            TaxNumber = user.TaxNumber
        };
    }

    public async Task UpdateProfileAsync(
        Guid userId,
        UpdateUserProfileDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            throw new KeyNotFoundException(
                $"User with id {userId} was not found.");
        }

        user.FirstName = NormalizeOptional(dto.FirstName);
        user.LastName = NormalizeOptional(dto.LastName);
        user.PhoneNumber = NormalizeOptional(dto.PhoneNumber);
        user.CompanyName = NormalizeOptional(dto.CompanyName);
        user.TaxNumber = NormalizeOptional(dto.TaxNumber);

        if ((user.CompanyName is null) != (user.TaxNumber is null))
            throw new ArgumentException("Céges számlázáshoz a cégnév és az adószám együttes megadása szükséges.");

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task<UserAddressesDto?> GetAddressesAsync(Guid userId)
    {
        var user = await _userManager.Users
            .Include(u => u.ShippingAddress)
            .Include(u => u.BillingAddress)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            return null;
        }

        return new UserAddressesDto
        {
            ShippingAddress = user.ShippingAddress?.ToDetailsDto(),
            BillingAddress = user.BillingAddress?.ToDetailsDto()
        };
    }

    public async Task UpdateAddressesAsync(
        Guid userId,
        UpdateUserAddressesDto dto)
    {
        var user = await _userManager.Users
            .Include(u => u.ShippingAddress)
            .Include(u => u.BillingAddress)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            throw new KeyNotFoundException(
                $"User with id {userId} was not found.");
        }

        if (dto.ShippingAddress is null)
        {
            throw new ArgumentException("A szállítási cím megadása kötelező.");
        }

        ValidateAddress(dto.ShippingAddress);
        var previousShippingAddressId = user.ShippingAddressId;
        var shippingAddress = await UpsertAddressAsync(
            user.ShippingAddress,
            dto.ShippingAddress);

        await _unitOfWork.SaveChangesAsync();
        user.ShippingAddressId = shippingAddress.Id;

        if (dto.UseShippingAsBilling)
        {
            user.BillingAddressId = shippingAddress.Id;
        }
        else
        {
            if (dto.BillingAddress is null)
            {
                throw new ArgumentException("A számlázási cím megadása kötelező.");
            }

            ValidateAddress(dto.BillingAddress);

            var existingBillingAddress = user.BillingAddressId == previousShippingAddressId
                ? null
                : user.BillingAddress;
            var billingAddress = await UpsertAddressAsync(
                existingBillingAddress,
                dto.BillingAddress);

            await _unitOfWork.SaveChangesAsync();
            user.BillingAddressId = billingAddress.Id;
        }

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task<Address> UpsertAddressAsync(
        Address? existingAddress,
        AddressCreateDto changes)
    {
        if (existingAddress is null)
        {
            var newAddress = changes.ToEntity();
            await _unitOfWork.AddressRepository.AddAsync(newAddress);
            return newAddress;
        }

        existingAddress.ApplyChanges(new AddressEditDto
        {
            PostalCode = changes.PostalCode,
            City = changes.City,
            Street = changes.Street,
            HouseNumber = changes.HouseNumber,
            Building = changes.Building,
            Floor = changes.Floor,
            Door = changes.Door,
            Doorbell = changes.Doorbell
        });
        _unitOfWork.AddressRepository.Update(existingAddress);

        return existingAddress;
    }

    private static void ValidateAddress(AddressCreateDto address)
    {
        if (string.IsNullOrWhiteSpace(address.PostalCode)
            || string.IsNullOrWhiteSpace(address.City)
            || string.IsNullOrWhiteSpace(address.Street)
            || string.IsNullOrWhiteSpace(address.HouseNumber))
        {
            throw new ArgumentException(
                "Az irányítószám, település, közterület és házszám megadása kötelező.");
        }
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
