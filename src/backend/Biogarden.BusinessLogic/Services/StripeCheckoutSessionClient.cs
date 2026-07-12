using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Payments;
using Stripe;
using Stripe.Checkout;

namespace Biogarden.BusinessLogic.Services;

public sealed class StripeCheckoutSessionClient : IStripeCheckoutSessionClient
{
    public async Task<StripeCheckoutSessionResult> CreateAsync(
        SessionCreateOptions options,
        string secretKey,
        CancellationToken cancellationToken = default)
    {
        var service = new SessionService(new StripeClient(secretKey));
        var session = await service.CreateAsync(
            options,
            cancellationToken: cancellationToken);

        return new StripeCheckoutSessionResult(
            session.Id,
            session.Url);
    }
}
