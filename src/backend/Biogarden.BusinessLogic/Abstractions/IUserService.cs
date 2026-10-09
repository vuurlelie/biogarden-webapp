using Biogarden.BusinessLogic.DTOs.Users;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IUserService
{
    Task<UserProfileDto?> GetProfileAsync(Guid userId);

    Task UpdateProfileAsync(Guid userId, UpdateUserProfileDto updateUserProfileDto);

    Task<UserAddressesDto?> GetAddressesAsync(Guid userId);

    Task UpdateAddressesAsync(Guid userId, UpdateUserAddressesDto updateUserAddressesDto);

    Task<AccountLifecycleResultDto> RequestAccountLifecycleAsync(Guid userId, AccountLifecycleRequestDto request, CancellationToken cancellationToken = default);
}
