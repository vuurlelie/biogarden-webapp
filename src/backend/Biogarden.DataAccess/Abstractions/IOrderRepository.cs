using Biogarden.DataAccess.Entities;

namespace Biogarden.DataAccess.Abstractions;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdWithItemsAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdWithItemsAndCartAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<Order?> GetByOrderNumberAndEmailWithItemsAsync(
        string orderNumber,
        string customerEmail,
        CancellationToken cancellationToken = default);

    Task<Order?> GetByCartIdWithItemsAsync(Guid cartId, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdempotencyKeyWithItemsAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<Order?> GetBySumUpCheckoutIdAsync(
        string sumUpCheckoutId,
        CancellationToken cancellationToken = default);

    Task<Order?> GetBySumUpTransactionIdAsync(
        string sumUpTransactionId,
        CancellationToken cancellationToken = default);

    Task<List<Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task AddWithdrawalRequestAsync(
        OrderWithdrawalRequest request,
        CancellationToken cancellationToken = default);

    void Update(Order order);
}
