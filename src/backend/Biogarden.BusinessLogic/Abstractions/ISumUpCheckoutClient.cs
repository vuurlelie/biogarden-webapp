using Biogarden.BusinessLogic.Models.Payments;

namespace Biogarden.BusinessLogic.Abstractions;

public interface ISumUpCheckoutClient
{
    Task<IReadOnlyList<SumUpCheckoutResult>> FindAsync(string reference, string apiKey, Uri apiBaseUrl, CancellationToken cancellationToken = default);
    Task<SumUpCheckoutResult> CreateAsync(
        SumUpCreateCheckoutRequest request,
        string apiKey,
        Uri apiBaseUrl,
        CancellationToken cancellationToken = default);

    Task<SumUpCheckoutResult> GetAsync(
        string checkoutId,
        string apiKey,
        Uri apiBaseUrl,
        CancellationToken cancellationToken = default);
}
