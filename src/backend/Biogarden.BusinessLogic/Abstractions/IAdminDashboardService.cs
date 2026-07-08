using Biogarden.BusinessLogic.DTOs.Admin;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IAdminDashboardService
{
    Task<AdminDashboardDto> GetAsync(CancellationToken cancellationToken = default);
}
