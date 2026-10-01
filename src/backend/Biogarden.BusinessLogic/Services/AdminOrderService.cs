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
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;

    public AdminOrderService(
        IAppDbContext databaseContext,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService)
    {
        _databaseContext = databaseContext;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
    }

    public async Task<IReadOnlyList<OrderDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _databaseContext.Orders.AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.WithdrawalRequests)
            .OrderByDescending(order => order.CreatedAtUtc).ToListAsync(cancellationToken);
        return orders.Select(order => order.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<AdminOrderWithdrawalRequestDto>> GetWithdrawalRequestsAsync(CancellationToken cancellationToken = default)
    {
        return await _databaseContext.OrderWithdrawalRequests.AsNoTracking()
            .Include(request => request.Order)
            .OrderBy(request => request.Status)
            .ThenByDescending(request => request.RequestedAtUtc)
            .Select(request => new AdminOrderWithdrawalRequestDto
            {
                Id = request.Id,
                OrderId = request.OrderId,
                OrderNumber = request.Order == null ? string.Empty : request.Order.OrderNumber,
                OrderStatus = request.Order == null ? OrderStatus.Pending : request.Order.Status,
                OrderTotalPrice = request.Order == null ? 0 : request.Order.TotalPrice,
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                Reason = request.Reason,
                Status = request.Status,
                RequestedAtUtc = request.RequestedAtUtc,
                HandledAtUtc = request.HandledAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<OrderDto> UpdateAsync(Guid orderId, AdminOrderUpdateDto changes, CancellationToken cancellationToken = default)
    {
        var order = await _databaseContext.Orders
            .Include(item => item.Items)
            .Include(item => item.WithdrawalRequests)
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order not found.");
        AdminEditVersion.RequireCurrent(changes.Version, order.RowVersion);
        var originalStatus = order.Status;

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
            order.DeletedAtUtc = order.CancelledAtUtc;
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
        var updatedOrder = order.ToDto();

        if (originalStatus != updatedOrder.Status)
        {
            var message = _emailTemplateService.BuildOrderStatusChangedMessage(
                updatedOrder.CustomerEmail,
                updatedOrder);
            _unitOfWork.OrderEmailRepository.Add(new Biogarden.DataAccess.Entities.OrderEmail
            {
                Id = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                OrderId = order.Id,
                Kind = "status",
                Recipient = message.To,
                Subject = message.Subject,
                HtmlBody = message.HtmlBody,
                PlainTextBody = message.PlainTextBody,
                NextAttemptAtUtc = DateTime.UtcNow
            });
        }

        // The state change and its notification commit atomically in one SaveChanges.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return order.ToDto();
    }

    public async Task<AdminOrderWithdrawalRequestDto> MarkWithdrawalRequestHandledAsync(
        int requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await _databaseContext.OrderWithdrawalRequests
            .Include(item => item.Order)
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Withdrawal request not found.");

        if (request.Status != OrderWithdrawalRequestStatus.Handled)
        {
            request.Status = OrderWithdrawalRequestStatus.Handled;
            request.HandledAtUtc = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new AdminOrderWithdrawalRequestDto
        {
            Id = request.Id,
            OrderId = request.OrderId,
            OrderNumber = request.Order?.OrderNumber ?? string.Empty,
            OrderStatus = request.Order?.Status ?? OrderStatus.Pending,
            OrderTotalPrice = request.Order?.TotalPrice ?? 0,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            Reason = request.Reason,
            Status = request.Status,
            RequestedAtUtc = request.RequestedAtUtc,
            HandledAtUtc = request.HandledAtUtc
        };
    }
}
