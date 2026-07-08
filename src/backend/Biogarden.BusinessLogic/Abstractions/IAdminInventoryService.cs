using Biogarden.BusinessLogic.DTOs.Admin;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IAdminInventoryService
{
    Task<IReadOnlyList<AdminInventoryItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AdminInventoryItemDto> UpdateAsync(int productId, UpdateAdminInventoryDto changes, CancellationToken cancellationToken = default);
}
