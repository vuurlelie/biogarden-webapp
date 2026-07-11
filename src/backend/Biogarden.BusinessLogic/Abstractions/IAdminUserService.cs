using Biogarden.BusinessLogic.DTOs.Admin;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<AdminUserDto> SetAdminRoleAsync(Guid userId, bool isAdmin, Guid currentAdminId, CancellationToken cancellationToken = default);

    Task<AdminUserDto> ConfirmEmailAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AdminUserDto> DeactivateAsync(Guid userId, Guid currentAdminId, CancellationToken cancellationToken = default);

    Task<AdminUserDto> ReactivateAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AdminUserErasureResultDto> EraseAsync(Guid userId, Guid currentAdminId, CancellationToken cancellationToken = default);

    Task<AdminUserDto> CompleteErasureRequestAsync(int requestId, Guid currentAdminId, AdminAccountLifecycleDecisionDto decision, CancellationToken cancellationToken = default);

    Task<AdminUserDto> RejectRequestAsync(int requestId, Guid currentAdminId, AdminAccountLifecycleDecisionDto decision, CancellationToken cancellationToken = default);
}
