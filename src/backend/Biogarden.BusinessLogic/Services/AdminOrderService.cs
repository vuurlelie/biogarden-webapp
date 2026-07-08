using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.BusinessLogic.Services;

public sealed class AdminOrderService : IAdminOrderService
{
    private readonly IAppDbContext _databaseContext;
    private readonly IUnitOfWork _unitOfWork;
    public AdminOrderService(IAppDbContext databaseContext, IUnitOfWork unitOfWork) { _databaseContext = databaseContext; _unitOfWork = unitOfWork; }

    public async Task<IReadOnlyList<OrderDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _databaseContext.Orders.AsNoTracking().Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAtUtc).ToListAsync(cancellationToken);
        return orders.Select(order => order.ToDto()).ToList();
    }

    public async Task<OrderDto> UpdateAsync(Guid orderId, AdminOrderUpdateDto changes, CancellationToken cancellationToken = default)
    {
        var order = await _databaseContext.Orders.Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order not found.");

        if (order.Status == OrderStatus.Cancelled && changes.Status != OrderStatus.Cancelled)
            throw new InvalidOperationException("A lemondott rendelés állapota nem módosítható.");
        if (order.Status == OrderStatus.Completed && changes.Status != OrderStatus.Completed)
            throw new InvalidOperationException("A teljesített rendelés állapota nem módosítható.");
        if (changes.Status == OrderStatus.Cancelled && order.Status is OrderStatus.Shipped or OrderStatus.Completed)
            throw new InvalidOperationException("A kiszállított vagy teljesített rendelés már nem mondható le.");
        if (changes.Status != OrderStatus.Cancelled && changes.Status < order.Status)
            throw new InvalidOperationException("A rendelés nem állítható vissza egy korábbi állapotba.");

        if (changes.Status == OrderStatus.Cancelled && order.Status != OrderStatus.Cancelled)
        {
            if (order.StockCommitted)
            {
                foreach (var item in order.Items)
                {
                    var product = await _unitOfWork.ProductRepository.GetByIdAsync(item.ProductId, cancellationToken);
                    if (product is not null) product.StockQuantity += item.Quantity;
                }
                order.StockCommitted = false;
            }
            order.CancelledAtUtc = DateTime.UtcNow;
            order.CancellationReason = string.IsNullOrWhiteSpace(changes.CancellationReason) ? null : changes.CancellationReason.Trim();
            if (order.PaymentStatus == PaymentStatus.Paid) changes.PaymentStatus = PaymentStatus.RefundPending;
        }

        if (changes.Status >= OrderStatus.Confirmed && changes.Status != OrderStatus.Cancelled && !order.StockCommitted)
        {
            foreach (var item in order.Items)
            {
                var product = await _unitOfWork.ProductRepository.GetByIdAsync(item.ProductId, cancellationToken)
                    ?? throw new InvalidOperationException("A rendelés egyik terméke már nem található.");
                product.StockQuantity -= item.Quantity;
                if (product.StockQuantity < 0) throw new InvalidOperationException($"Nincs elegendő készlet: {item.ProductName}.");
            }
            order.StockCommitted = true;
        }

        if (order.Status == OrderStatus.Pending && changes.Status >= OrderStatus.Confirmed && changes.Status != OrderStatus.Cancelled)
            order.ConfirmedAtUtc = DateTime.UtcNow;
        order.Status = changes.Status;
        order.PaymentStatus = changes.PaymentStatus;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return order.ToDto();
    }
}
