using Biogarden.DataAccess.Entities;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IAccountLifecycleOperations
{
    Task DeactivateAsync(ApplicationUser user, CancellationToken cancellationToken);
    Task ReactivateAsync(ApplicationUser user, CancellationToken cancellationToken);
    Task AnonymizeAsync(ApplicationUser user, CancellationToken cancellationToken);
    Task DeleteWithoutOrdersAsync(ApplicationUser user, CancellationToken cancellationToken);
}
