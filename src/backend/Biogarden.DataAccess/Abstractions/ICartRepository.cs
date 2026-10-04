using Biogarden.DataAccess.Entities;

namespace Biogarden.DataAccess.Abstractions;

public interface ICartRepository
{
    Task<IAsyncDisposable> AcquireOwnerLockAsync(Guid? userId, string? sessionId, CancellationToken cancellationToken = default);
    Task<Cart?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Cart?> GetActiveBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<Cart?> GetLatestCheckedOutByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Cart?> GetLatestCheckedOutBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<Cart?> GetByIdWithItemsAsync(Guid cartId, CancellationToken cancellationToken = default);

    Task AddAsync(Cart cart, CancellationToken cancellationToken = default);

    Task AddItemAsync(CartItem cartItem, CancellationToken cancellationToken = default);
    void Update(Cart cart);
    void Remove(Cart cart);

    Task<int> GetActiveReservedQuantityForProductAsync(
        int productId,
        Guid? excludedCartId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, int>> GetActiveReservedQuantitiesAsync(
        IReadOnlyCollection<int> productIds, Guid? excludedCartId = null, Guid? additionalExcludedCartId = null,
        CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default);

    Task<int> GetActiveReservedQuantityForProductAsync(
        int productId, Guid excludedCartId, Guid additionalExcludedCartId,
        CancellationToken cancellationToken = default);
}
