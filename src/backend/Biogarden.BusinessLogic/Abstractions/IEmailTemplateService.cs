using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.DTOs.Contact;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IEmailTemplateService
{
    EmailMessage BuildEmailConfirmationMessage(string recipientEmail, string confirmationUrl);

    EmailMessage BuildPasswordResetMessage(string recipientEmail, string resetUrl);

    EmailMessage BuildPasswordChangedMessage(string recipientEmail);

    EmailMessage BuildAccountDeactivatedMessage(string recipientEmail);

    EmailMessage BuildAccountReactivatedMessage(string recipientEmail);

    EmailMessage BuildAccountDeletedMessage(string recipientEmail);

    EmailMessage BuildAccountAnonymizedMessage(string recipientEmail);

    EmailMessage BuildContactBusinessNotificationMessage(
        string recipientEmail,
        ContactMessageRequestDto request);

    EmailMessage BuildContactVisitorConfirmationMessage(
        string recipientEmail,
        ContactMessageRequestDto request);
}
