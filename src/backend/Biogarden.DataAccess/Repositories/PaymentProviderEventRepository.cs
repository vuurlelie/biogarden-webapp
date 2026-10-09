using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Repositories;

public sealed class PaymentProviderEventRepository : IPaymentProviderEventRepository
{
    private readonly IAppDbContext _context;

    public PaymentProviderEventRepository(IAppDbContext context)
    {
        _context = context;
    }

    public Task<PaymentProviderEvent?> GetByProviderAndEventKeyAsync(string provider, string eventKey, CancellationToken cancellationToken = default)
    {
        return _context.PaymentProviderEvents.FirstOrDefaultAsync(paymentEvent => paymentEvent.Provider == provider && paymentEvent.EventKey == eventKey, cancellationToken);
    }

    public Task AddAsync(PaymentProviderEvent paymentEvent, CancellationToken cancellationToken = default)
    {
        return _context.PaymentProviderEvents.AddAsync(paymentEvent, cancellationToken).AsTask();
    }
}
