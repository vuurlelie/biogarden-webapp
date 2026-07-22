using Biogarden.DataAccess.Entities;

namespace Biogarden.DataAccess.Abstractions;

public interface IPaymentProviderEventRepository
{
    Task<PaymentProviderEvent?> GetByProviderAndEventKeyAsync(
        string provider,
        string eventKey,
        CancellationToken cancellationToken = default);

    Task AddAsync(PaymentProviderEvent paymentEvent, CancellationToken cancellationToken = default);
}
