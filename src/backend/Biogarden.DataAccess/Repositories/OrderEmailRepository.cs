using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Repositories;

public sealed class OrderEmailRepository(IAppDbContext database) : IOrderEmailRepository
{
    public void Add(OrderEmail email) => database.OrderEmails.Add(email);

    public async Task<OrderEmail?> TryClaimAsync(DateTime now, CancellationToken token)
    {
        var due = database.OrderEmails.Where(x => x.SentAtUtc == null &&
            x.NextAttemptAtUtc <= now && (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= now));
        var email = await due.AsNoTracking().OrderBy(x => x.NextAttemptAtUtc).ThenBy(x => x.Id)
            .FirstOrDefaultAsync(token);
        if (email is null) return null;

        var leaseId = Guid.NewGuid();
        var updated = await due.Where(x => x.Id == email.Id).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.LeaseId, leaseId)
            .SetProperty(x => x.LeaseUntilUtc, now.AddMinutes(2))
            .SetProperty(x => x.Attempts, x => x.Attempts + 1), token);
        if (updated == 0) return null;

        email.LeaseId = leaseId;
        email.Attempts++;
        return email;
    }

    public async Task CompleteAsync(Guid id, Guid leaseId, DateTime now, CancellationToken token)
    {
        await database.OrderEmails.Where(x => x.Id == id && x.LeaseId == leaseId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.SentAtUtc, now)
                .SetProperty(x => x.LeaseId, (Guid?)null)
                .SetProperty(x => x.LeaseUntilUtc, (DateTime?)null)
                .SetProperty(x => x.Recipient, string.Empty)
                .SetProperty(x => x.Subject, string.Empty)
                .SetProperty(x => x.HtmlBody, (string?)null)
                .SetProperty(x => x.PlainTextBody, (string?)null), token);
    }

    public async Task RetryAsync(Guid id, Guid leaseId, DateTime nextAttempt, CancellationToken token)
    {
        await database.OrderEmails.Where(x => x.Id == id && x.LeaseId == leaseId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.NextAttemptAtUtc, nextAttempt)
                .SetProperty(x => x.LeaseId, (Guid?)null)
                .SetProperty(x => x.LeaseUntilUtc, (DateTime?)null), token);
    }
}
