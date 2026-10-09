namespace Biogarden.BusinessLogic.Abstractions;

public interface ISumUpCheckoutStatusService
{
    Task HandleAsync(string payload, CancellationToken cancellationToken = default);
}
