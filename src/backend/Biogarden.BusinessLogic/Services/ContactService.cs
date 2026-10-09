using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Contact;
using Biogarden.BusinessLogic.Options;
using Microsoft.Extensions.Options;

namespace Biogarden.BusinessLogic.Services;

public sealed class ContactService : IContactService
{
    private readonly INotificationEmailRepository _repository;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly EmailBrandingOptions _brandingOptions;

    public ContactService(INotificationEmailRepository repository, IEmailTemplateService emailTemplateService, IOptions<EmailBrandingOptions> brandingOptions)
    {
        _repository = repository;
        _emailTemplateService = emailTemplateService;
        _brandingOptions = brandingOptions.Value;
    }

    public async Task SendMessageAsync(ContactMessageRequestDto contactMessageRequestDto, CancellationToken cancellationToken = default)
    {
        if (contactMessageRequestDto.SubmissionId == Guid.Empty)
            throw new ArgumentException("Az üzenetazonosító megadása kötelező.");
        var normalizedRequest = Normalize(contactMessageRequestDto);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(normalizedRequest))));
        var now = DateTime.UtcNow;

        var businessMessage = _emailTemplateService.BuildContactBusinessNotificationMessage(_brandingOptions.ContactEmail, normalizedRequest);


        var visitorMessage = _emailTemplateService.BuildContactVisitorConfirmationMessage(normalizedRequest.Email, normalizedRequest);
        await _repository.AcceptContactAsync([Create("contact-business", businessMessage), Create("contact-visitor", visitorMessage)], cancellationToken);

        NotificationEmail Create(string kind, EmailMessage message) => new()
        {
            EventId = contactMessageRequestDto.SubmissionId,
            Kind = kind,
            RequestHash = hash,
            Recipient = message.To,
            Subject = message.Subject,
            HtmlBody = message.HtmlBody,
            PlainTextBody = message.PlainTextBody,
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            ExpiresAtUtc = now.AddDays(30)
        };
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
