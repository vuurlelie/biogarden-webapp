using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Models.Payments;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IStripeCheckoutService
{
    Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
        OrderDto order,
        CancellationToken cancellationToken = default);
}
