using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Biogarden.BusinessLogic.Services;

public sealed class UnconfirmedAccountCleanupService(
    IAppDbContext database, UserManager<ApplicationUser> users, TimeProvider clock,
    IOptions<UnconfirmedAccountCleanupOptions> options, IOptions<AuthEmailOptions> emailOptions,
    ILogger<UnconfirmedAccountCleanupService> logger)
{
    public async Task<int> RunBatchAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        settings.ValidateForStartup();
        if (!settings.Enabled)
            return 0;
        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-emailOptions.Value.UnconfirmedAccountRetentionDays);
        var ids = await EligibleUsers(cutoff).OrderBy(user => user.CreatedAt).ThenBy(user => user.Id)
            .Select(user => user.Id).Take(settings.BatchSize).ToArrayAsync(cancellationToken);
        var deleted = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                deleted += await NotificationTransaction.RunAsync(database, async () =>
                {
                    var context = (DbContext)database;
                    // Same SQL resource as account-security mutations; transaction ownership
                    // releases the lock on commit, rollback, cancellation and process failure.
                    await context.Database.ExecuteSqlRawAsync("""
                        DECLARE @result int;
                        EXEC @result = sys.sp_getapplock
                            @Resource = N'Biogarden.AccountSecurity', @LockMode = N'Exclusive',
                            @LockOwner = N'Transaction', @LockTimeout = 5000;
                        IF @result < 0 THROW 51000, 'Account cleanup lock unavailable.', 1;
                        """, cancellationToken);
                    // Recheck after acquiring the lock: another worker or confirmation may have won.
                    var user = await EligibleUsers(cutoff).SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
                    if (user is null)
                        return 0;
                    var addressIds = new[] { user.ShippingAddressId, user.BillingAddressId }.OfType<int>().Distinct().ToArray();
                    var carts = await database.Carts.Include(cart => cart.Items)
                        .Where(cart => cart.UserId == id && !cart.IsCheckedOut).ToListAsync(cancellationToken);
                    database.Carts.RemoveRange(carts);
                    await database.SaveChangesAsync(cancellationToken);
                    var result = await users.DeleteAsync(user);
                    if (!result.Succeeded)
                        throw new InvalidOperationException("Identity rejected expired account deletion.");
                    await SavedAddressCleanup.DeleteUnreferencedAsync(database, users.Users, addressIds, cancellationToken);
                    return 1;
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                // Failed deletes roll back and remain eligible on the next scheduled batch.
                // Never log email, address, SQL parameters or Identity error descriptions.
                logger.LogError("Expired account cleanup failed ({ErrorType}); deletion will be retried.", exception.GetType().Name);
            }
        }
        return deleted;
    }

    private IQueryable<ApplicationUser> EligibleUsers(DateTime cutoff) => users.Users.Where(user =>
        !user.EmailConfirmed && user.CreatedAt < cutoff && user.RecordStatus == RecordStatus.Active
        && user.DeactivatedAtUtc == null && user.AnonymizedAtUtc == null
        && !database.Orders.Any(order => order.UserId == user.Id)
        && !database.UserRoles.Any(role => role.UserId == user.Id)
        && !database.Carts.Any(cart => cart.UserId == user.Id && cart.IsCheckedOut)
        && !database.AccountLifecycleRequests.Any(request => request.UserId == user.Id || request.HandledByAdminId == user.Id));
}
