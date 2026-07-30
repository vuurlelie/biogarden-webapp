using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.DTOs.Orders;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IAdminOrderService
{
    Task<IReadOnlyList<OrderDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminOrderWithdrawalRequestDto>> GetWithdrawalRequestsAsync(CancellationToken cancellationToken = default);
    Task<OrderDto> UpdateAsync(Guid orderId, AdminOrderUpdateDto changes, CancellationToken cancellationToken = default);
    Task<AdminOrderWithdrawalRequestDto> MarkWithdrawalRequestHandledAsync(int requestId, CancellationToken cancellationToken = default);
}
