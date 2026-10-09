using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.DTOs.Common;
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

    public async Task<PagedResultDto<OrderDto>> GetPageAsync(AdminOrderListQuery filter, CancellationToken cancellationToken = default)
    {
        var query = _databaseContext.Orders.AsNoTracking();
        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) query = query.Where(x => x.OrderNumber.Contains(search) || x.CustomerName.Contains(search) || x.CustomerEmail.Contains(search));
        if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status);
        if (filter.Fulfillment.HasValue) query = query.Where(x => x.FulfillmentMethod == filter.Fulfillment);
        var count = await query.CountAsync(cancellationToken);
        var page = AdminPaging.Page(filter, count);
        var orders = await query
            .OrderByDescending(order => order.CreatedAtUtc).ThenBy(order => order.Id)
            .Skip((page - 1) * filter.NormalizedPageSize).Take(filter.NormalizedPageSize)
            .Include(order => order.Items)
            .Include(order => order.WithdrawalRequests)
            .AsSplitQuery().ToListAsync(cancellationToken);
        return new(orders.Select(order => order.ToDto()).ToList(), page, filter.NormalizedPageSize, count);
    }

    public async Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _databaseContext.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.WithdrawalRequests)
            .AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return order?.ToDto();
    }

    public async Task<PagedResultDto<AdminOrderRequestThreadDto>> GetWithdrawalRequestsAsync(AdminWithdrawalListQuery filter, CancellationToken cancellationToken = default)
    {
        var query = _databaseContext.OrderWithdrawalRequests.AsNoTracking();
        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) query = query.Where(x => x.CustomerName.Contains(search) || x.CustomerEmail.Contains(search) || x.Reason.Contains(search) || (x.Order != null && x.Order.OrderNumber.Contains(search)));
        if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status);
        var groups = query.GroupBy(request => request.OrderId).Select(group => new
        {
            OrderId = group.Key,
            Latest = group.Max(request => request.RequestedAtUtc)
        });
        var count = await groups.CountAsync(cancellationToken);
        var page = AdminPaging.Page(filter, count);
        var orderIds = await groups.OrderByDescending(group => group.Latest).ThenBy(group => group.OrderId)
            .Skip((page - 1) * filter.NormalizedPageSize).Take(filter.NormalizedPageSize)
            .Select(group => group.OrderId).ToListAsync(cancellationToken);
        // Filters select conversations; include their complete history for context.
        var messages = await _databaseContext.OrderWithdrawalRequests.AsNoTracking()
            .Where(request => orderIds.Contains(request.OrderId))
            .OrderBy(request => request.RequestedAtUtc).ThenBy(request => request.Id)
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
                AdminResponse = request.AdminResponse,
                HandledAtUtc = request.HandledAtUtc
            })
            .ToListAsync(cancellationToken);
        var byOrder = messages.ToLookup(message => message.OrderId);
        var items = orderIds.Select(id => new AdminOrderRequestThreadDto
        {
            OrderId = id, Messages = byOrder[id].ToList()
        }).ToList();
        return new(items, page, filter.NormalizedPageSize, count);
    }

    public async Task<OrderDto> UpdateAsync(Guid orderId, AdminOrderUpdateDto changes, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var order = await _databaseContext.Orders
            .Include(item => item.Items)
            .Include(item => item.WithdrawalRequests)
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order not found.");
        AdminEditVersion.RequireCurrent(changes.Version, order.RowVersion);
        var originalStatus = order.Status;
        var nextPayment = changes.Status == OrderStatus.Cancelled && order.Status != OrderStatus.Cancelled && order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.PartiallyRefunded
            ? PaymentStatus.RefundPending : changes.PaymentStatus;
        AdminPaymentTransition.Validate(order, nextPayment);
        if (changes.Status == OrderStatus.Cancelled && nextPayment == PaymentStatus.Paid)
            throw new InvalidOperationException("Lemondáskor nem rögzíthető új fizetés.");
        if (nextPayment != order.PaymentStatus)
        {
            if (actorId is null || actorId == Guid.Empty) throw new UnauthorizedAccessException("A módosító adminisztrátor azonosítója szükséges.");
            var reason = changes.PaymentCorrectionReason?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
                throw new ArgumentException("A fizetési módosítás 1–500 karakteres indoklása kötelező.");
            _databaseContext.OrderPaymentCorrections.Add(new()
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ActorId = actorId.Value,
                CreatedAtUtc = DateTime.UtcNow,
                PreviousStatus = order.PaymentStatus,
                NewStatus = nextPayment,
                Reason = reason
            });
        }

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
        }

        if (changes.Status >= OrderStatus.Confirmed && changes.Status != OrderStatus.Cancelled && !order.StockCommitted)
        {
            foreach (var item in order.Items)
            {
                var product = await _unitOfWork.ProductRepository.GetByIdAsync(item.ProductId, cancellationToken)
                    ?? throw new InvalidOperationException("A rendelés egyik terméke már nem található.");
                await StockUpdatePolicy.ValidateAsync(_unitOfWork, product, product.StockQuantity - item.Quantity, cancellationToken);
                product.StockQuantity -= item.Quantity;
                if (product.StockQuantity < 0) throw new InvalidOperationException($"Nincs elegendő készlet: {item.ProductName}.");
            }
            order.StockCommitted = true;
        }

        if (order.Status == OrderStatus.Pending && changes.Status >= OrderStatus.Confirmed && changes.Status != OrderStatus.Cancelled)
            order.ConfirmedAtUtc = DateTime.UtcNow;
        if (order.Status != OrderStatus.Completed && changes.Status == OrderStatus.Completed)
            order.CompletedAtUtc = DateTime.UtcNow;
        order.Status = changes.Status;
        order.PaymentStatus = nextPayment;
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
        CancellationToken cancellationToken = default, string? adminResponse = null)
    {
        adminResponse = string.IsNullOrWhiteSpace(adminResponse) ? null : adminResponse.Trim();
        if (adminResponse?.Length > 1000) throw new ArgumentException("A válasz legfeljebb 1000 karakter lehet.");
        var request = await _databaseContext.OrderWithdrawalRequests
            .Include(item => item.Order)
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Withdrawal request not found.");

        if (request.Status == OrderWithdrawalRequestStatus.Handled && request.AdminResponse != adminResponse)
            throw new InvalidOperationException("A kérést már feldolgozták. Frissítsd az oldalt a mentett válasz megtekintéséhez.");
        if (request.Status != OrderWithdrawalRequestStatus.Handled)
        {
            request.Status = OrderWithdrawalRequestStatus.Handled;
            request.HandledAtUtc = DateTime.UtcNow;
            request.AdminResponse = adminResponse;
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
            AdminResponse = request.AdminResponse,
            HandledAtUtc = request.HandledAtUtc
        };
    }
}
