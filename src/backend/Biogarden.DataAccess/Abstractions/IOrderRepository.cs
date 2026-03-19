using Biogarden.DataAccess.Entities;

namespace Biogarden.DataAccess.Abstractions;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdWithItemsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<List<Order>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    void Update(Order order);
}