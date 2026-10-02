using Biogarden.BusinessLogic.Models.Email;
using Biogarden.DataAccess.Entities;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IAccountNotificationQueue
{
    Task QueueAuthenticationAsync(ApplicationUser user, string kind, CancellationToken token = default);
    Task QueueMessageAsync(EmailMessage message, string kind, CancellationToken token = default);
}
