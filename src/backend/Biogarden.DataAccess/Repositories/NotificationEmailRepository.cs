using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Repositories;

public sealed class NotificationEmailRepository(IAppDbContext database) : INotificationEmailRepository
{
    public async Task AcceptContactAsync(IReadOnlyList<NotificationEmail> emails, CancellationToken token)
    {
        var first = emails[0];
        var existing = await database.NotificationEmails.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventId == first.EventId && x.Kind == first.Kind, token);
        if (existing is not null) { CheckReplay(existing, first); return; }
        database.NotificationEmails.AddRange(emails);
        try { await database.SaveChangesAsync(token); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            foreach (var email in emails) ((DbContext)database).Entry(email).State = EntityState.Detached;
            existing = await database.NotificationEmails.AsNoTracking()
                .SingleAsync(x => x.EventId == first.EventId && x.Kind == first.Kind, token);
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
        var due = database.NotificationEmails.Where(x => x.CompletedAtUtc == null && x.ExpiresAtUtc > now &&
            x.NextAttemptAtUtc <= now && (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= now));
        var email = await due.AsNoTracking().OrderBy(x => x.NextAttemptAtUtc).ThenBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).FirstOrDefaultAsync(token);
        if (email is null) return null;
        var leaseId = Guid.NewGuid();
        var count = await due.Where(x => x.Id == email.Id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.LeaseId, leaseId).SetProperty(x => x.LeaseUntilUtc, now.AddMinutes(2))
            .SetProperty(x => x.Attempts, x => x.Attempts + 1), token);
        if (count == 0) return null;
        email.LeaseId = leaseId;
        email.Attempts++;
        return email;
    }

    public Task CompleteAsync(Guid id, Guid leaseId, DateTime now, string outcome, CancellationToken token)
        => database.NotificationEmails.Where(x => x.Id == id && x.LeaseId == leaseId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.CompletedAtUtc, now).SetProperty(x => x.Outcome, outcome)
            .SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseUntilUtc, (DateTime?)null)
            .SetProperty(x => x.Recipient, string.Empty).SetProperty(x => x.Subject, string.Empty)
            .SetProperty(x => x.HtmlBody, (string?)null).SetProperty(x => x.PlainTextBody, (string?)null)
            .SetProperty(x => x.SecurityStamp, (string?)null).SetProperty(x => x.UserId, (Guid?)null), token);

    public Task RetryAsync(Guid id, Guid leaseId, DateTime nextAttempt, CancellationToken token)
        => database.NotificationEmails.Where(x => x.Id == id && x.LeaseId == leaseId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.NextAttemptAtUtc, nextAttempt)
            .SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseUntilUtc, (DateTime?)null), token);

    public Task ExpireAsync(DateTime now, CancellationToken token)
    {
        var ids = database.NotificationEmails.Where(x => x.CompletedAtUtc == null && x.ExpiresAtUtc <= now &&
            (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= now)).OrderBy(x => x.ExpiresAtUtc).Take(100).Select(x => x.Id);
        return database.NotificationEmails.Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.CompletedAtUtc, now).SetProperty(x => x.Outcome, "expired")
            .SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseUntilUtc, (DateTime?)null)
            .SetProperty(x => x.Recipient, string.Empty).SetProperty(x => x.Subject, string.Empty)
            .SetProperty(x => x.HtmlBody, (string?)null).SetProperty(x => x.PlainTextBody, (string?)null)
            .SetProperty(x => x.SecurityStamp, (string?)null).SetProperty(x => x.UserId, (Guid?)null), token);
    }
}
