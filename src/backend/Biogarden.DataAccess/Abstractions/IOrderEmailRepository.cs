using Biogarden.DataAccess.Entities;

namespace Biogarden.DataAccess.Abstractions;

public interface IOrderEmailRepository
{
    void Add(OrderEmail email);
    Task<OrderEmail?> TryClaimAsync(DateTime now, CancellationToken token);
    Task CompleteAsync(Guid id, Guid leaseId, DateTime now, CancellationToken token);
    Task RetryAsync(Guid id, Guid leaseId, DateTime nextAttempt, CancellationToken token);
}
