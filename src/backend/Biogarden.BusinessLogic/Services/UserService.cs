using Biogarden.BusinessLogic.Abstractions;
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
            PhoneNumber = user.PhoneNumber
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
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            throw new KeyNotFoundException(
                $"User with id {userId} was not found.");
        }

        if (dto.ShippingAddressId.HasValue)
        {
            await EnsureAddressExists(dto.ShippingAddressId.Value);
        }

        if (dto.BillingAddressId.HasValue)
        {
            await EnsureAddressExists(dto.BillingAddressId.Value);
        }

        user.ShippingAddressId = dto.ShippingAddressId;
        user.BillingAddressId = dto.BillingAddressId;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task EnsureAddressExists(int addressId)
    {
        var address = await _unitOfWork.AddressRepository.GetByIdAsync(addressId);

        if (address is null)
        {
            throw new KeyNotFoundException(
                $"Address with id {addressId} was not found.");
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