using Biogarden.BusinessLogic.Models.Alerting;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IErrorAlertService
{
    Task NotifyAsync(
        ErrorAlert alert,
        CancellationToken cancellationToken = default);
}
