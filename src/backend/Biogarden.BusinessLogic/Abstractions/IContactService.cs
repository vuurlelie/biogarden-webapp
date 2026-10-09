using Biogarden.BusinessLogic.DTOs.Contact;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IContactService
{
    Task SendMessageAsync(ContactMessageRequestDto contactMessageRequestDto, CancellationToken cancellationToken = default);
}
