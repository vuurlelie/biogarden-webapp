using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Contact;
using Biogarden.BusinessLogic.Options;
using Microsoft.Extensions.Options;

namespace Biogarden.BusinessLogic.Services;

public sealed class ContactService : IContactService
{
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly EmailBrandingOptions _brandingOptions;

    public ContactService(
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IOptions<EmailBrandingOptions> brandingOptions)
    {
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _brandingOptions = brandingOptions.Value;
    }

    public async Task SendMessageAsync(
        ContactMessageRequestDto contactMessageRequestDto,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = Normalize(contactMessageRequestDto);

        var businessMessage = _emailTemplateService.BuildContactBusinessNotificationMessage(
            _brandingOptions.ContactEmail,
            normalizedRequest);
        await _emailService.SendAsync(businessMessage, cancellationToken);

        var visitorMessage = _emailTemplateService.BuildContactVisitorConfirmationMessage(
            normalizedRequest.Email,
            normalizedRequest);
        await _emailService.SendAsync(visitorMessage, cancellationToken);
    }

    private static ContactMessageRequestDto Normalize(ContactMessageRequestDto request)
    {
        return new ContactMessageRequestDto
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
            Message = request.Message.Trim()
        };
    }
}
