using Biogarden.BusinessLogic.Models.Payments;
using Stripe.Checkout;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IStripeCheckoutSessionClient
{
    Task<StripeCheckoutSessionResult> CreateAsync(
        SessionCreateOptions options,
        string secretKey,
        CancellationToken cancellationToken = default);
}
