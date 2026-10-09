using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Runtime.ExceptionServices;

namespace Biogarden.DataAccess.Repositories;

public sealed class NotificationEmailRepository(IAppDbContext database) : INotificationEmailRepository
{
    public async Task AcceptContactAsync(IReadOnlyList<NotificationEmail> emails, CancellationToken token)
    {
        var context = (DbContext)database;
        // A duplicate pair can deadlock while inserting its two unique event/kind rows.
        // Retry the whole idempotent acceptance, including the persisted replay check.
        // Keep this policy local: order/payment transactions have their own recovery rules.
        var strategy = new SqlServerRetryingExecutionStrategy(context, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(1), errorNumbersToAdd: [1205]);
        await strategy.ExecuteAsync(async cancellationToken =>
        {
            foreach (var email in emails) context.Entry(email).State = EntityState.Detached;
            try { await AcceptContactCoreAsync(emails, cancellationToken); }
            catch (InvalidOperationException exception) when (exception.InnerException is DbUpdateException { InnerException: SqlException })
            {
                // The application's non-retrying SQL strategy wraps transient save failures.
                // Let this local strategy classify the original database error, not the wrapper.
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }, token);
    }

    private async Task AcceptContactCoreAsync(IReadOnlyList<NotificationEmail> emails, CancellationToken token)
    {
        var first = emails[0];
        var existing = await database.NotificationEmails.AsNoTracking()
            .FirstOrDefaultAsync(notificationEmail => notificationEmail.EventId == first.EventId && notificationEmail.Kind == first.Kind, token);
        if (existing is not null)
        {
            CheckReplay(existing, first);
            return;
        }
        database.NotificationEmails.AddRange(emails);
        try { await database.SaveChangesAsync(token); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            foreach (var email in emails) ((DbContext)database).Entry(email).State = EntityState.Detached;
            existing = await database.NotificationEmails.AsNoTracking()
                .SingleAsync(notificationEmail => notificationEmail.EventId == first.EventId && notificationEmail.Kind == first.Kind, token);
            CheckReplay(existing, first);
        }
    }

    private static void CheckReplay(NotificationEmail existing, NotificationEmail proposed)
    {
        if (existing.RequestHash != proposed.RequestHash)
            throw new InvalidOperationException("Az üzenetazonosító már más tartalomhoz tartozik. Indíts új üzenetet.");
    }

    public async Task<NotificationEmail?> TryClaimAsync(DateTime now, CancellationToken token)
    {
        var due = database.NotificationEmails.Where(notificationEmail => notificationEmail.CompletedAtUtc == null && notificationEmail.ExpiresAtUtc > now &&
            notificationEmail.NextAttemptAtUtc <= now && (notificationEmail.LeaseUntilUtc == null || notificationEmail.LeaseUntilUtc <= now));
        var email = await due.AsNoTracking().OrderBy(notificationEmail => notificationEmail.NextAttemptAtUtc).ThenBy(notificationEmail => notificationEmail.CreatedAtUtc).ThenBy(notificationEmail => notificationEmail.Id).FirstOrDefaultAsync(token);
        if (email is null)
            return null;
        var leaseId = Guid.NewGuid();
        var count = await due.Where(notificationEmail => notificationEmail.Id == email.Id).ExecuteUpdateAsync(
            updateSettersBuilder => updateSettersBuilder
                .SetProperty(notificationEmail => notificationEmail.LeaseId, leaseId).SetProperty(notificationEmail => notificationEmail.LeaseUntilUtc, now.AddMinutes(2))
                .SetProperty(notificationEmail => notificationEmail.Attempts, notificationEmail => notificationEmail.Attempts + 1),
            token
        );
        if (count == 0)
            return null;
        email.LeaseId = leaseId;
        email.Attempts++;
        return email;
    }

    public Task CompleteAsync(Guid id, Guid leaseId, DateTime now, string outcome, CancellationToken token)
        => database.NotificationEmails.Where(notificationEmail => notificationEmail.Id == id && notificationEmail.LeaseId == leaseId).ExecuteUpdateAsync(
            updateSettersBuilder => updateSettersBuilder
                        .SetProperty(notificationEmail => notificationEmail.CompletedAtUtc, now).SetProperty(notificationEmail => notificationEmail.Outcome, outcome)
                        .SetProperty(notificationEmail => notificationEmail.LeaseId, (Guid? )null).SetProperty(notificationEmail => notificationEmail.LeaseUntilUtc, (DateTime? )null)
                        .SetProperty(notificationEmail => notificationEmail.Recipient, string.Empty).SetProperty(notificationEmail => notificationEmail.Subject, string.Empty)
                        .SetProperty(notificationEmail => notificationEmail.HtmlBody, (string? )null).SetProperty(notificationEmail => notificationEmail.PlainTextBody, (string? )null)
                        .SetProperty(notificationEmail => notificationEmail.SecurityStamp, (string? )null).SetProperty(notificationEmail => notificationEmail.UserId, (Guid? )null),
            token
        );

    public Task RetryAsync(Guid id, Guid leaseId, DateTime nextAttempt, CancellationToken token)
        => database.NotificationEmails.Where(notificationEmail => notificationEmail.Id == id && notificationEmail.LeaseId == leaseId).ExecuteUpdateAsync(
            updateSettersBuilder => updateSettersBuilder
                        .SetProperty(notificationEmail => notificationEmail.NextAttemptAtUtc, nextAttempt)
                        .SetProperty(notificationEmail => notificationEmail.LeaseId, (Guid? )null).SetProperty(notificationEmail => notificationEmail.LeaseUntilUtc, (DateTime? )null),
            token
        );

    public Task ExpireAsync(DateTime now, CancellationToken token)
    {
        var ids = database.NotificationEmails.Where(notificationEmail => notificationEmail.CompletedAtUtc == null && notificationEmail.ExpiresAtUtc <= now &&
            (notificationEmail.LeaseUntilUtc == null || notificationEmail.LeaseUntilUtc <= now)).OrderBy(notificationEmail => notificationEmail.ExpiresAtUtc).Take(100).Select(notificationEmail => notificationEmail.Id);
        return database.NotificationEmails.Where(notificationEmail => ids.Contains(notificationEmail.Id)).ExecuteUpdateAsync(
            updateSettersBuilder => updateSettersBuilder
                        .SetProperty(notificationEmail => notificationEmail.CompletedAtUtc, now).SetProperty(notificationEmail => notificationEmail.Outcome, "expired")
                        .SetProperty(notificationEmail => notificationEmail.LeaseId, (Guid? )null).SetProperty(notificationEmail => notificationEmail.LeaseUntilUtc, (DateTime? )null)
                        .SetProperty(notificationEmail => notificationEmail.Recipient, string.Empty).SetProperty(notificationEmail => notificationEmail.Subject, string.Empty)
                        .SetProperty(notificationEmail => notificationEmail.HtmlBody, (string? )null).SetProperty(notificationEmail => notificationEmail.PlainTextBody, (string? )null)
                        .SetProperty(notificationEmail => notificationEmail.SecurityStamp, (string? )null).SetProperty(notificationEmail => notificationEmail.UserId, (Guid? )null),
            token
        );
    }
}
