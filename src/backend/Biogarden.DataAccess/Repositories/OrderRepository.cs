using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Repositories;

public class OrderRepository : IOrderRepository
{
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

    public async Task<Order?> GetByOrderNumberAndEmailWithItemsAsync(
        string orderNumber,
        string customerEmail,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(order => order.Items)
            .ThenInclude(orderItem => orderItem.Product)
            .Include(order => order.WithdrawalRequests)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                order => order.OrderNumber == orderNumber &&
                    order.CustomerEmail.ToLower() == customerEmail,
                cancellationToken);
    }

    public async Task<Order?> GetByCartIdWithItemsAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(order => order.Items)
            .ThenInclude(orderItem => orderItem.Product)
            .AsSplitQuery()
            .FirstOrDefaultAsync(order => order.CartId == cartId, cancellationToken);
    }

    public async Task<Order?> GetByIdempotencyKeyWithItemsAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(order => order.Items)
            .ThenInclude(orderItem => orderItem.Product)
            .Include(order => order.Cart)
            .AsSplitQuery()
            .FirstOrDefaultAsync(order => order.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<Order?> GetBySumUpCheckoutIdAsync(
        string sumUpCheckoutId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(order => order.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                order => order.SumUpCheckoutId == sumUpCheckoutId || _context.PaymentAttempts.Any(attempt => attempt.OrderId == order.Id && attempt.CheckoutId == sumUpCheckoutId),
                cancellationToken);
    }

    public async Task<Order?> GetBySumUpTransactionIdAsync(
        string sumUpTransactionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(order => order.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                order => order.SumUpTransactionId == sumUpTransactionId,
                cancellationToken);
    }

    public async Task<List<Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(order => order.Items)
            .Include(order => order.WithdrawalRequests)
            .AsSplitQuery()
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _context.Orders.AddAsync(order, cancellationToken);
    }

    public async Task AddWithdrawalRequestAsync(
        OrderWithdrawalRequest request,
        CancellationToken cancellationToken = default)
    {
        await _context.OrderWithdrawalRequests.AddAsync(request, cancellationToken);
    }

    public void Update(Order order)
    {
        _context.Orders.Update(order);
    }
}
