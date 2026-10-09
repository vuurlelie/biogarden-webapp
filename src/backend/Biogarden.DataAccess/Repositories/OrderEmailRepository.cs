using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Repositories;

public sealed class OrderEmailRepository(IAppDbContext database) : IOrderEmailRepository
{
    public void Add(OrderEmail email) => database.OrderEmails.Add(email);

    public async Task<OrderEmail?> TryClaimAsync(DateTime now, CancellationToken token)
    {
        var due = database.OrderEmails.Where(orderEmail => orderEmail.SentAtUtc == null &&
            orderEmail.NextAttemptAtUtc <= now && (orderEmail.LeaseUntilUtc == null || orderEmail.LeaseUntilUtc <= now));
        var email = await due.AsNoTracking().OrderBy(orderEmail => orderEmail.NextAttemptAtUtc).ThenBy(orderEmail => orderEmail.Id)
            .FirstOrDefaultAsync(token);
        if (email is null)
            return null;

        var leaseId = Guid.NewGuid();
        var updated = await due.Where(orderEmail => orderEmail.Id == email.Id).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(orderEmail => orderEmail.LeaseId, leaseId)
                .SetProperty(orderEmail => orderEmail.LeaseUntilUtc, now.AddMinutes(2))
                .SetProperty(orderEmail => orderEmail.Attempts, orderEmail => orderEmail.Attempts + 1),
            token
        );
        if (updated == 0)
            return null;

        email.LeaseId = leaseId;
        email.Attempts++;
        return email;
    }

    public async Task CompleteAsync(Guid id, Guid leaseId, DateTime now, CancellationToken token)
    {
        await database.OrderEmails.Where(orderEmail => orderEmail.Id == id && orderEmail.LeaseId == leaseId)
            .ExecuteUpdateAsync(
                setters => setters
                                .SetProperty(orderEmail => orderEmail.SentAtUtc, now)
                                .SetProperty(orderEmail => orderEmail.LeaseId, (Guid? )null)
                                .SetProperty(orderEmail => orderEmail.LeaseUntilUtc, (DateTime? )null)
                                .SetProperty(orderEmail => orderEmail.Recipient, string.Empty)
                                .SetProperty(orderEmail => orderEmail.Subject, string.Empty)
                                .SetProperty(orderEmail => orderEmail.HtmlBody, (string? )null)
                                .SetProperty(orderEmail => orderEmail.PlainTextBody, (string? )null),
                token
            );
    }

    public async Task RetryAsync(Guid id, Guid leaseId, DateTime nextAttempt, CancellationToken token)
    {
        await database.OrderEmails.Where(orderEmail => orderEmail.Id == id && orderEmail.LeaseId == leaseId)
            .ExecuteUpdateAsync(
                setters => setters
                                .SetProperty(orderEmail => orderEmail.NextAttemptAtUtc, nextAttempt)
                                .SetProperty(orderEmail => orderEmail.LeaseId, (Guid? )null)
                                .SetProperty(orderEmail => orderEmail.LeaseUntilUtc, (DateTime? )null),
                token
            );
    }
}
