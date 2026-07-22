using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Models.Payments;

namespace Biogarden.BusinessLogic.Abstractions;

public interface ISumUpCheckoutService
{
    Task<SumUpCheckoutResult> CreateCheckoutAsync(
        OrderDto order,
        CancellationToken cancellationToken = default);

    Task<SumUpCheckoutResult> GetCheckoutAsync(
        string checkoutId,
        CancellationToken cancellationToken = default);
}
