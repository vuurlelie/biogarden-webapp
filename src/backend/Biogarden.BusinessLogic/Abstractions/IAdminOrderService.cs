using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.DTOs.Orders;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IAdminOrderService
{
    Task<PagedResultDto<OrderDto>> GetPageAsync(AdminOrderListQuery query, CancellationToken cancellationToken = default);
    Task<PagedResultDto<AdminOrderWithdrawalRequestDto>> GetWithdrawalRequestsAsync(AdminWithdrawalListQuery query, CancellationToken cancellationToken = default);
    Task<OrderDto> UpdateAsync(Guid orderId, AdminOrderUpdateDto changes, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<AdminOrderWithdrawalRequestDto> MarkWithdrawalRequestHandledAsync(int requestId, CancellationToken cancellationToken = default);
    Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
