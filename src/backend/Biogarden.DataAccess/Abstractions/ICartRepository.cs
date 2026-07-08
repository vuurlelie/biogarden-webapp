using Biogarden.DataAccess.Entities;

namespace Biogarden.DataAccess.Abstractions;

public interface ICartRepository
{
    Task<Cart?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Cart?> GetActiveBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<Cart?> GetByIdWithItemsAsync(Guid cartId, CancellationToken cancellationToken = default);

    Task AddAsync(Cart cart, CancellationToken cancellationToken = default);

    Task AddItemAsync(CartItem cartItem, CancellationToken cancellationToken = default);
    void Update(Cart cart);
    void Remove(Cart cart);

    Task<int> GetActiveReservedQuantityForProductAsync(
        int productId,
        Guid? excludedCartId = null,
        CancellationToken cancellationToken = default);

    Task<List<Cart>> GetExpiredCartsAsync(CancellationToken cancellationToken = default);
}
