using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.BusinessLogic.DTOs.Admin;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IAdminInventoryService
{
    Task<PagedResultDto<AdminInventoryItemDto>> GetPageAsync(AdminInventoryListQuery query, CancellationToken cancellationToken = default);
    Task<AdminInventoryItemDto> UpdateAsync(int productId, UpdateAdminInventoryDto changes, CancellationToken cancellationToken = default);
    Task<AdminInventoryItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
