using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.BusinessLogic.Services;

public sealed class AdminUserService : IAdminUserService
{
    private readonly IAppDbContext _databaseContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountLifecycleOperations _lifecycle;

    public AdminUserService(IAppDbContext databaseContext, UserManager<ApplicationUser> userManager, IAccountLifecycleOperations lifecycle)
    {
        _databaseContext = databaseContext;
        _userManager = userManager;
        _lifecycle = lifecycle;
    }

    public async Task<PagedResultDto<AdminUserDto>> GetPageAsync(AdminUserListQuery filter, CancellationToken cancellationToken = default)
    {
        var query = _userManager.Users.AsNoTracking()
            .Where(candidateUser => candidateUser.AnonymizedAtUtc == null && (candidateUser.Email == null || !candidateUser.Email.EndsWith("@deleted.invalid")));
        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(candidateUser =>
            ((candidateUser.LastName ?? "") + " " + (candidateUser.FirstName ?? "")).Contains(search)
            || (candidateUser.Email != null && candidateUser.Email.Contains(search)) || (candidateUser.PhoneNumber != null && candidateUser.PhoneNumber.Contains(search))
            || (candidateUser.CompanyName != null && candidateUser.CompanyName.Contains(search)) || (candidateUser.TaxNumber != null && candidateUser.TaxNumber.Contains(search)));
        var adminIds = _databaseContext.UserRoles.Where(userRole => _databaseContext.Roles.Any(identityRole => identityRole.Id == userRole.RoleId && identityRole.NormalizedName == "ADMIN")).Select(userRole => userRole.UserId);
        if (filter.Role == "admin")
            query = query.Where(candidateUser => adminIds.Contains(candidateUser.Id));
        if (filter.Role == "customer")
            query = query.Where(candidateUser => !adminIds.Contains(candidateUser.Id));
        if (filter.Status == "active")
            query = query.Where(candidateUser => candidateUser.DeactivatedAtUtc == null);
        if (filter.Status == "inactive")
            query = query.Where(candidateUser => candidateUser.DeactivatedAtUtc != null);
        if (filter.Status == "request")
            query = query.Where(candidateUser => _databaseContext.AccountLifecycleRequests.Any(accountLifecycleRequest => accountLifecycleRequest.UserId == candidateUser.Id && accountLifecycleRequest.Status == AccountLifecycleRequestStatus.Pending));
        var count = await query.CountAsync(cancellationToken);
        var page = AdminPaging.Page(filter, count);
        var users = await query
            .OrderByDescending(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * filter.NormalizedPageSize).Take(filter.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(user => user.Id).ToArray();
        var orderCounts = await _databaseContext.Orders
            .AsNoTracking()
            .Where(order => order.UserId.HasValue && userIds.Contains(order.UserId.Value))
            .Select(order => new
            {
                order.UserId,
                order.Status
            })
            .GroupBy(order => order.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                OrderCount = group.Count(),
                ActiveOrderCount = group.Count(order => order.Status == OrderStatus.Pending
                    || order.Status == OrderStatus.Confirmed
                    || order.Status == OrderStatus.Shipped)
            })
            .ToDictionaryAsync(item => item.UserId.GetValueOrDefault(), cancellationToken);

        var pendingRequests = await _databaseContext.AccountLifecycleRequests
            .AsNoTracking()
            .Where(request => userIds.Contains(request.UserId)
                && request.Status == AccountLifecycleRequestStatus.Pending)
            .GroupBy(request => request.UserId)
            .Select(group => group.OrderByDescending(request => request.RequestedAtUtc).First())
            .ToDictionaryAsync(request => request.UserId, cancellationToken);

        var result = new List<AdminUserDto>(users.Count);
        var pageRoles = await (from membership in _databaseContext.UserRoles
                               join role in _databaseContext.Roles on membership.RoleId equals role.Id
                               where userIds.Contains(membership.UserId)
                               select new { membership.UserId, role.Name }).ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            var roles = pageRoles.Where(candidateUserRole => candidateUserRole.UserId == user.Id && candidateUserRole.Name != null).Select(candidateUserRole => candidateUserRole.Name!).ToList();
            orderCounts.TryGetValue(user.Id, out var counts);
            pendingRequests.TryGetValue(user.Id, out var pendingRequest);

            result.Add(ToDto(user, roles, counts?.OrderCount ?? 0, counts?.ActiveOrderCount ?? 0, pendingRequest));
        }

        return new(result, page, filter.NormalizedPageSize, count);
    }

    public async Task<AdminUserDto> SetAdminRoleAsync(Guid userId, bool isAdmin, Guid currentAdminId, CancellationToken cancellationToken = default)
    {
        if (!isAdmin && userId == currentAdminId)
        {
            throw new InvalidOperationException("A saját admin jogosultságodat nem veheted el.");
        }

        var user = await FindUserAsync(userId, cancellationToken);

        if (isAdmin && (user.RecordStatus != RecordStatus.Active || user.DeactivatedAtUtc is not null ||
            user.AnonymizedAtUtc is not null || !user.EmailConfirmed ||
            (user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow)))
        {
            throw new InvalidOperationException("Csak aktív, megerősített fiók kaphat admin jogosultságot.");
        }

        var hasRole = await _userManager.IsInRoleAsync(user, "Admin");
        IdentityResult result = IdentityResult.Success;

        // Identity persists the user and role membership together in its SaveChanges call.
        if (hasRole != isAdmin)
            user.SecurityStamp = Guid.NewGuid().ToString();

        if (isAdmin && !hasRole)
        {
            result = await _userManager.AddToRoleAsync(user, "Admin");
        }
        else if (!isAdmin && hasRole)
        {
            result = await _userManager.RemoveFromRoleAsync(user, "Admin");
        }

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Description)));
        }

        return await BuildUserDtoAsync(user.Id, cancellationToken);
    }

    public async Task<AdminUserDto> ConfirmEmailAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(userId, cancellationToken);

        if (user.AnonymizedAtUtc is not null || user.RecordStatus == RecordStatus.Deleted)
        {
            throw new InvalidOperationException("Törölt vagy anonimizált fiók email címe nem erősíthető meg.");
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Description)));
            }
        }

        return await BuildUserDtoAsync(user.Id, cancellationToken);
    }

    public Task<AdminUserDto> DeactivateAsync(Guid userId, Guid currentAdminId, CancellationToken cancellationToken = default)
        => NotificationTransaction.RunAsync(_databaseContext, () => DeactivateAsyncCore(userId, currentAdminId, cancellationToken), cancellationToken);

    private async Task<AdminUserDto> DeactivateAsyncCore(Guid userId, Guid currentAdminId, CancellationToken cancellationToken = default)
    {
        if (userId == currentAdminId)
        {
            throw new InvalidOperationException("A saját fiókodat nem deaktiválhatod az admin felületről.");
        }

        var user = await FindUserAsync(userId, cancellationToken);
        await _lifecycle.DeactivateAsync(user, cancellationToken);

        return await BuildUserDtoAsync(user.Id, cancellationToken);
    }

    public Task<AdminUserDto> ReactivateAsync(Guid userId, CancellationToken cancellationToken = default)
        => NotificationTransaction.RunAsync(_databaseContext, () => ReactivateAsyncCore(userId, cancellationToken), cancellationToken);

    private async Task<AdminUserDto> ReactivateAsyncCore(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(userId, cancellationToken);
        await _lifecycle.ReactivateAsync(user, cancellationToken);

        return await BuildUserDtoAsync(user.Id, cancellationToken);
    }

    public Task<AdminUserErasureResultDto> EraseAsync(Guid userId, Guid currentAdminId, CancellationToken cancellationToken = default)
        => NotificationTransaction.RunAsync(_databaseContext, () => EraseAsyncCore(userId, currentAdminId, cancellationToken), cancellationToken);

    private async Task<AdminUserErasureResultDto> EraseAsyncCore(Guid userId, Guid currentAdminId, CancellationToken cancellationToken = default)
    {
        if (userId == currentAdminId)
        {
            throw new InvalidOperationException("A saját fiókodat nem törölheted az admin felületről.");
        }

        var user = await FindUserAsync(userId, cancellationToken);
        var activeOrderCount = await CountActiveOrdersAsync(user.Id, cancellationToken);
        if (activeOrderCount > 0)
        {
            throw new InvalidOperationException("A felhasználónak folyamatban lévő rendelése van, ezért a fiók most nem törölhető vagy anonimizálható.");
        }

        var orderCount = await _databaseContext.Orders
            .CountAsync(order => order.UserId == user.Id, cancellationToken);

        if (orderCount == 0)
        {
            await CompletePendingErasureRequestsAsync(user.Id, currentAdminId, "Admin által törölve.", cancellationToken);
            await _lifecycle.DeleteWithoutOrdersAsync(user, cancellationToken);


            return new AdminUserErasureResultDto
            {
                UserId = user.Id,
                User = null,
                Message = "A fiókot és a mentett személyes adatokat törölted."
            };
        }

        await _lifecycle.AnonymizeAsync(user, cancellationToken);

        await CompletePendingErasureRequestsAsync(user.Id, currentAdminId, "Admin által anonimizálva.", cancellationToken);

        return new AdminUserErasureResultDto
        {
            UserId = user.Id,
            User = await BuildUserDtoAsync(user.Id, cancellationToken),
            Message = "A fiók mentett személyes adatait anonimizáltad. A korábbi rendelési és számlázási adatok megmaradtak."
        };
    }

    public Task<AdminUserDto> CompleteErasureRequestAsync(int requestId, Guid currentAdminId, AdminAccountLifecycleDecisionDto decision, CancellationToken cancellationToken = default)
        => NotificationTransaction.RunAsync(_databaseContext, () => CompleteErasureRequestAsyncCore(requestId, currentAdminId, decision, cancellationToken), cancellationToken);

    private async Task<AdminUserDto> CompleteErasureRequestAsyncCore(int requestId, Guid currentAdminId, AdminAccountLifecycleDecisionDto decision, CancellationToken cancellationToken = default)
    {
        var request = await _databaseContext.AccountLifecycleRequests
            .Include("User.ShippingAddress")
            .Include("User.BillingAddress")
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Account lifecycle request not found.");

        if (request.Type != AccountLifecycleRequestType.Erasure)
        {
            throw new InvalidOperationException("Csak törlési kérelem zárható anonimizálással.");
        }

        if (request.Status != AccountLifecycleRequestStatus.Pending)
        {
            throw new InvalidOperationException("Ez a kérelem már feldolgozásra került.");
        }

        var user = request.User ?? throw new KeyNotFoundException("User not found.");
        if (user.Id == currentAdminId)
        {
            throw new InvalidOperationException("A saját fiókodat nem törölheted vagy anonimizálhatod az admin felületről.");
        }

        var activeOrderCount = await CountActiveOrdersAsync(user.Id, cancellationToken);

        if (activeOrderCount > 0)
        {
            throw new InvalidOperationException("A felhasználónak még van folyamatban lévő rendelése.");
        }

        await _lifecycle.AnonymizeAsync(user, cancellationToken);


        request.Status = AccountLifecycleRequestStatus.Completed;
        request.HandledAtUtc = DateTime.UtcNow;
        request.HandledByAdminId = currentAdminId;
        request.AdminNote = NormalizeOptional(decision.AdminNote);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return await BuildUserDtoAsync(user.Id, cancellationToken);
    }

    public async Task<AdminUserDto> RejectRequestAsync(int requestId, Guid currentAdminId, AdminAccountLifecycleDecisionDto decision, CancellationToken cancellationToken = default)
    {
        var request = await _databaseContext.AccountLifecycleRequests
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Account lifecycle request not found.");

        if (request.Status != AccountLifecycleRequestStatus.Pending)
        {
            throw new InvalidOperationException("Ez a kérelem már feldolgozásra került.");
        }

        request.Status = AccountLifecycleRequestStatus.Rejected;
        request.HandledAtUtc = DateTime.UtcNow;
        request.HandledByAdminId = currentAdminId;
        request.AdminNote = NormalizeOptional(decision.AdminNote);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return await BuildUserDtoAsync(request.UserId, cancellationToken);
    }

    private async Task<ApplicationUser> FindUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _userManager.Users
            .Include(user => user.ShippingAddress)
            .Include(user => user.BillingAddress)
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");
    }

    private async Task<AdminUserDto> BuildUserDtoAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");
        var roles = await _userManager.GetRolesAsync(user);
        var orderCount = await _databaseContext.Orders.CountAsync(order => order.UserId == userId, cancellationToken);
        var activeOrderCount = await CountActiveOrdersAsync(userId, cancellationToken);
        var pendingRequest = await _databaseContext.AccountLifecycleRequests.AsNoTracking()
            .Where(request => request.UserId == userId
                && request.Status == AccountLifecycleRequestStatus.Pending)
            .OrderByDescending(request => request.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return ToDto(user, roles, orderCount, activeOrderCount, pendingRequest);
    }

    private static AdminUserDto ToDto(ApplicationUser user, IEnumerable<string> roles, int orderCount, int activeOrderCount, AccountLifecycleRequest? pendingRequest)
    {
        return new AdminUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            CompanyName = user.CompanyName,
            TaxNumber = user.TaxNumber,
            Roles = roles.ToList(),
            IsAdmin = roles.Any(role => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)),
            EmailConfirmed = user.EmailConfirmed,
            IsDeactivated = user.DeactivatedAtUtc is not null,
            IsAnonymized = user.AnonymizedAtUtc is not null,
            CreatedAt = user.CreatedAt,
            OrderCount = orderCount,
            ActiveOrderCount = activeOrderCount,
            PendingRequest = pendingRequest is null
                ? null
                : new AdminAccountLifecycleRequestDto
                {
                    Id = pendingRequest.Id,
                    Type = pendingRequest.Type,
                    Status = pendingRequest.Status,
                    Reason = pendingRequest.Reason,
                    Message = pendingRequest.Message,
                    RequestedAtUtc = pendingRequest.RequestedAtUtc
                }
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private Task<int> CountActiveOrdersAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _databaseContext.Orders.CountAsync(
            order => order.UserId == userId
                && (order.Status == OrderStatus.Pending
                    || order.Status == OrderStatus.Confirmed
                    || order.Status == OrderStatus.Shipped),
            cancellationToken
        );
    }

    private async Task CompletePendingErasureRequestsAsync(Guid userId, Guid currentAdminId, string adminNote, CancellationToken cancellationToken)
    {
        var requests = await _databaseContext.AccountLifecycleRequests
            .Where(request => request.UserId == userId
                && request.Type == AccountLifecycleRequestType.Erasure
                && request.Status == AccountLifecycleRequestStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var request in requests)
        {
            request.Status = AccountLifecycleRequestStatus.Completed;
            request.HandledAtUtc = DateTime.UtcNow;
            request.HandledByAdminId = currentAdminId;
            request.AdminNote = adminNote;
        }

        if (requests.Count > 0)
        {
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
    }

}
