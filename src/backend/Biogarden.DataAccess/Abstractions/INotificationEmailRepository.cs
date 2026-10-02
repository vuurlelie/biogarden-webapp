using Biogarden.DataAccess.Entities;

namespace Biogarden.DataAccess.Abstractions;

public interface INotificationEmailRepository
{
    Task AcceptContactAsync(IReadOnlyList<NotificationEmail> emails, CancellationToken token);
    Task<NotificationEmail?> TryClaimAsync(DateTime now, CancellationToken token);
    Task CompleteAsync(Guid id, Guid leaseId, DateTime now, string outcome, CancellationToken token);
    Task RetryAsync(Guid id, Guid leaseId, DateTime nextAttempt, CancellationToken token);
    Task ExpireAsync(DateTime now, CancellationToken token);
}
