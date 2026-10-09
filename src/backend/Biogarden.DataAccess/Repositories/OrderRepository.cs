using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Repositories;

public class OrderRepository : IOrderRepository
{
    private static readonly TimeZoneInfo BudapestTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");
    private readonly IAppDbContext _context;

    public OrderRepository(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    public async Task<Order?> GetByIdWithItemsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(order => order.Items)
            .ThenInclude(orderItem => orderItem.Product)
            .Include(order => order.WithdrawalRequests)
            .AsSplitQuery()
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    public async Task<Order?> GetByIdWithItemsAndCartAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(order => order.Items)
            .ThenInclude(orderItem => orderItem.Product)
            .Include(order => order.Cart)
            .AsSplitQuery()
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    public async Task<Order?> GetByOrderNumberAndEmailWithItemsAsync(string orderNumber, string customerEmail, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(order => order.Items)
            .ThenInclude(orderItem => orderItem.Product)
            .Include(order => order.WithdrawalRequests)
            .AsSplitQuery()
            .FirstOrDefaultAsync(order => order.OrderNumber == orderNumber && order.CustomerEmail.ToLower() == customerEmail, cancellationToken);
    }

    public async Task<Order?> GetByCartIdWithItemsAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(order => order.Items)
            .ThenInclude(orderItem => orderItem.Product)
            .AsSplitQuery()
            .FirstOrDefaultAsync(order => order.CartId == cartId, cancellationToken);
    }

    public async Task<Order?> GetByIdempotencyKeyWithItemsAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(order => order.Items)
            .ThenInclude(orderItem => orderItem.Product)
            .Include(order => order.Cart)
            .AsSplitQuery()
            .FirstOrDefaultAsync(order => order.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<Order?> GetBySumUpCheckoutIdAsync(string sumUpCheckoutId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(order => order.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                order => order.SumUpCheckoutId == sumUpCheckoutId || _context.PaymentAttempts.Any(attempt => attempt.OrderId == order.Id && attempt.CheckoutId == sumUpCheckoutId),
                cancellationToken
            );
    }

    public async Task<Order?> GetBySumUpTransactionIdAsync(string sumUpTransactionId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(order => order.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(order => order.SumUpTransactionId == sumUpTransactionId, cancellationToken);
    }

    public async Task<(List<Order> Items, int TotalCount, int Page)> GetByUserIdAsync(Guid userId, string view, string? search, int? year, int page, int pageSize, DateTime now, CancellationToken cancellationToken = default)
    {
        var completedCutoff = now.AddDays(-14);
        var cancelledCutoff = now.AddDays(-7);
        var query = _context.Orders.AsNoTracking().Where(order => order.UserId == userId);
        if (view == "recent")
            query = query.Where(order => order.WithdrawalRequests.Any(request => request.Status == Biogarden.DataAccess.Enums.OrderWithdrawalRequestStatus.Pending)
                || (order.Status != Biogarden.DataAccess.Enums.OrderStatus.Completed && order.Status != Biogarden.DataAccess.Enums.OrderStatus.Cancelled)
                || (order.Status == Biogarden.DataAccess.Enums.OrderStatus.Completed && order.CompletedAtUtc >= completedCutoff)
                || (order.Status == Biogarden.DataAccess.Enums.OrderStatus.Cancelled && (order.DeletedAtUtc ?? order.CancelledAtUtc) >= cancelledCutoff));
        else if (view == "history")
            query = query.Where(order => !order.WithdrawalRequests.Any(request => request.Status == Biogarden.DataAccess.Enums.OrderWithdrawalRequestStatus.Pending)
                && ((order.Status == Biogarden.DataAccess.Enums.OrderStatus.Completed && (order.CompletedAtUtc == null || order.CompletedAtUtc < completedCutoff))
                || (order.Status == Biogarden.DataAccess.Enums.OrderStatus.Cancelled && ((order.DeletedAtUtc ?? order.CancelledAtUtc) == null || (order.DeletedAtUtc ?? order.CancelledAtUtc) < cancelledCutoff))));
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(order => order.OrderNumber.Contains(search.Trim()));
        if (year.HasValue)
        {
            // Match the Budapest calendar year shown on customer order cards.
            // Convert the bounds once so SQL can still filter the UTC column directly.
            var localStart = new DateTime(year.Value, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var start = TimeZoneInfo.ConvertTimeToUtc(localStart, BudapestTimeZone);
            var end = TimeZoneInfo.ConvertTimeToUtc(localStart.AddYears(1), BudapestTimeZone);
            query = query.Where(order => order.CreatedAtUtc >= start && order.CreatedAtUtc < end);
        }
        var count = await query.CountAsync(cancellationToken);
        page = Math.Min(page, Math.Max(1, (int)Math.Ceiling((double)count / pageSize)));
        var items = await query.OrderByDescending(order => order.CreatedAtUtc).ThenBy(order => order.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(order => order.Items).Include(order => order.WithdrawalRequests).AsSplitQuery()
            .ToListAsync(cancellationToken);
        return (items, count, page);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _context.Orders.AddAsync(order, cancellationToken);
    }

    public async Task<int> CountPendingWithdrawalRequestsForUpdateAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        // Serialize submissions for this order until the surrounding transaction commits.
        if (_context is DbContext db && db.Database.IsSqlServer())
        {
            if (db.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Withdrawal request limits require a transaction.");
            await _context.Orders.FromSqlInterpolated($"SELECT * FROM Orders WITH (UPDLOCK, HOLDLOCK) WHERE Id = {orderId}")
                .AsNoTracking().SingleAsync(cancellationToken);
        }
        return await _context.OrderWithdrawalRequests.CountAsync(request => request.OrderId == orderId && request.Status == Biogarden.DataAccess.Enums.OrderWithdrawalRequestStatus.Pending, cancellationToken);
    }

    public async Task AddWithdrawalRequestAsync(OrderWithdrawalRequest request, CancellationToken cancellationToken = default)
    {
        await _context.OrderWithdrawalRequests.AddAsync(request, cancellationToken);
    }

    public void Update(Order order)
    {
        _context.Orders.Update(order);
    }
}
