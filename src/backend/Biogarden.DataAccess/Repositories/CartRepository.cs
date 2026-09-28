using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Repositories;

public class CartRepository : ICartRepository
{
    private readonly IAppDbContext _context;

    public CartRepository(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Cart?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Carts
            .Include(cart => cart.Items)
            .ThenInclude(cartItem => cartItem.Product)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                cart =>
                    cart.UserId == userId &&
                    !cart.IsCheckedOut &&
                    cart.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);
    }

    public async Task<Cart?> GetActiveBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.Carts
            .Include(cart => cart.Items)
            .ThenInclude(cartItem => cartItem.Product)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                cart =>
                    cart.SessionId == sessionId &&
                    !cart.IsCheckedOut &&
                    cart.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);
    }

    public async Task<Cart?> GetLatestCheckedOutByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Carts
            .Include(cart => cart.Items)
            .ThenInclude(cartItem => cartItem.Product)
            .AsSplitQuery()
            .Where(cart => cart.UserId == userId && cart.IsCheckedOut)
            .OrderByDescending(cart => cart.CheckedOutAtUtc ?? cart.LastActivityAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Cart?> GetLatestCheckedOutBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.Carts
            .Include(cart => cart.Items)
            .ThenInclude(cartItem => cartItem.Product)
            .AsSplitQuery()
            .Where(cart => cart.SessionId == sessionId && cart.IsCheckedOut)
            .OrderByDescending(cart => cart.CheckedOutAtUtc ?? cart.LastActivityAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Cart?> GetByIdWithItemsAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        return await _context.Carts
            .Include(cart => cart.Items)
            .ThenInclude(cartItem => cartItem.Product)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cart => cart.Id == cartId, cancellationToken);
    }

    public async Task AddAsync(Cart cart, CancellationToken cancellationToken = default)
    {
        await _context.Carts.AddAsync(cart, cancellationToken);
    }

    public async Task AddItemAsync(CartItem cartItem, CancellationToken cancellationToken = default)
    {
        await _context.CartItems.AddAsync(cartItem, cancellationToken);
    }

    public void Update(Cart cart)
    {
        _context.Carts.Update(cart);
    }

    public void Remove(Cart cart)
    {
        _context.Carts.Remove(cart);
    }

    public async Task<int> GetActiveReservedQuantityForProductAsync(int productId, Guid? excludedCartId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.CartItems
            .Where(cartItem =>
                cartItem.ProductId == productId &&
                cartItem.Cart != null &&
                !cartItem.Cart.IsCheckedOut &&
                cartItem.Cart.ExpiresAtUtc > DateTime.UtcNow);

        if (excludedCartId.HasValue)
        {
            query = query.Where(cartItem => cartItem.CartId != excludedCartId.Value);
        }

        return await query.SumAsync(cartItem => cartItem.Quantity, cancellationToken);
    }

    public Task<int> GetActiveReservedQuantityForProductAsync(
        int productId, Guid excludedCartId, Guid additionalExcludedCartId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return _context.CartItems.Where(item => item.ProductId == productId
            && item.Cart != null && !item.Cart.IsCheckedOut && item.Cart.ExpiresAtUtc > now
            && item.CartId != excludedCartId && item.CartId != additionalExcludedCartId)
            .SumAsync(item => item.Quantity, cancellationToken);
    }

    public async Task<List<Cart>> GetExpiredCartsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Carts
            .Include(cart => cart.Items)
            .Where(cart => !cart.IsCheckedOut && cart.ExpiresAtUtc <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);
    }
}
