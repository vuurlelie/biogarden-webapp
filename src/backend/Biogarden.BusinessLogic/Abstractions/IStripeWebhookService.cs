namespace Biogarden.BusinessLogic.Abstractions;

public interface IStripeWebhookService
{
    Task HandleAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken = default);
}
