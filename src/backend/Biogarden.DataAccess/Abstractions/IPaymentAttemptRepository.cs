using Biogarden.DataAccess.Entities;

namespace Biogarden.DataAccess.Abstractions;

public interface IPaymentAttemptRepository
{
    Task<IAsyncDisposable> AcquireLockAsync(Guid orderId, CancellationToken token = default);
    Task<PaymentAttempt?> GetLatestAsync(Guid orderId, CancellationToken token = default);
    Task<PaymentAttempt?> GetByReferenceAsync(string reference, CancellationToken token = default);
    Task<PaymentAttempt?> GetByCheckoutAsync(string checkoutId, CancellationToken token = default);
    void Add(PaymentAttempt attempt);
    Task<Guid?> FindOrderIdAsync(string checkoutId, string? reference, CancellationToken token = default);
}
