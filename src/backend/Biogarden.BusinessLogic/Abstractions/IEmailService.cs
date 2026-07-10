using Biogarden.BusinessLogic.Models.Email;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
