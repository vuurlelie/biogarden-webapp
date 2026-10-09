using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

namespace Biogarden.DataAccess.Repositories;

public class CartRepository : ICartRepository
{
    private readonly IAppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public CartRepository(IAppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<IAsyncDisposable> AcquireOwnerLockAsync(Guid? userId, string? sessionId, CancellationToken cancellationToken = default)
    {
        if (userId is null && string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Kosárhoz felhasználó vagy session azonosító szükséges.");
        // Guest IDs issued by the API are GUIDs. Case normalization also matches SQL's
        // case-insensitive session lookup; hash the value to keep lock names bounded.
        var owner = userId.HasValue ? $"user:{userId:N}" : $"guest:{sessionId!.Trim().ToUpperInvariant()}";
        var resource = "Biogarden.Cart:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner)));
        var database = (DbContext)_context;
        var settings = new SqlConnectionStringBuilder(database.Database.GetConnectionString()) { Pooling = false };
        var connection = new SqlConnection(settings.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource = @resource,
                    @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 5000;
                SELECT @result;
                """;
            command.Parameters.AddWithValue("@resource", resource);
            var result = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
            if (result < 0)
                throw new InvalidOperationException("Másik kosárművelet van folyamatban. Kérjük, próbáld újra.");
            // Keep ownership through the entire operation, including initial creation.
            // An unpooled connection releases the session lock when disposed, even on failure.
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<Cart?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return await _context.Carts
            .Include(cart => cart.Items)
            .ThenInclude(cartItem => cartItem.Product)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cart => cart.UserId == userId && !cart.IsCheckedOut && cart.ExpiresAtUtc > now, cancellationToken);
    }

    public async Task<Cart?> GetActiveBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return await _context.Carts
            .Include(cart => cart.Items)
            .ThenInclude(cartItem => cartItem.Product)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cart => cart.UserId == null && cart.SessionId == sessionId && !cart.IsCheckedOut && cart.ExpiresAtUtc > now, cancellationToken);
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
            .Where(cart => cart.UserId == null && cart.SessionId == sessionId && cart.IsCheckedOut)
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
        => (await GetActiveReservedQuantitiesAsync([productId], excludedCartId, null, cancellationToken))
            .GetValueOrDefault(productId);

    public async Task<int> GetActiveReservedQuantityForProductAsync(int productId, Guid excludedCartId, Guid additionalExcludedCartId, CancellationToken cancellationToken = default)
        => (await GetActiveReservedQuantitiesAsync([productId], excludedCartId, additionalExcludedCartId, cancellationToken))
            .GetValueOrDefault(productId);

    public async Task<IReadOnlyDictionary<int, int>> GetActiveReservedQuantitiesAsync(IReadOnlyCollection<int> productIds, Guid? excludedCartId = null, Guid? additionalExcludedCartId = null, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return new Dictionary<int, int>();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var ids = productIds.Distinct().ToArray();
        var query = _context.CartItems.Where(item => ids.Contains(item.ProductId)
            && item.Cart != null && !item.Cart.IsCheckedOut && item.Cart.ExpiresAtUtc > now);
        if (excludedCartId.HasValue)
            query = query.Where(item => item.CartId != excludedCartId.Value);
        if (additionalExcludedCartId.HasValue)
            query = query.Where(item => item.CartId != additionalExcludedCartId.Value);

        return await query.GroupBy(item => item.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToDictionaryAsync(item => item.ProductId, item => item.Quantity, cancellationToken);
    }

    public Task<int> DeleteExpiredBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        // Never let a caller bypass the retention boundary with a future cutoff.
        if (cutoffUtc >= _timeProvider.GetUtcNow().UtcDateTime)
            throw new ArgumentOutOfRangeException(nameof(cutoffUtc));

        // SQL applies eligibility and deletion in one statement. No stale tracked snapshot,
        // no loading item graphs. The FK also protects a cart linked to an order concurrently.
        return _context.Carts.Where(cart => !cart.IsCheckedOut && cart.ExpiresAtUtc <= cutoffUtc
                && !_context.Orders.Any(order => order.CartId == cart.Id))
            .OrderBy(cart => cart.ExpiresAtUtc).ThenBy(cart => cart.Id)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
