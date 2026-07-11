using Biogarden.BusinessLogic.Models.Email;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IEmailTemplateService
{
    EmailMessage BuildEmailConfirmationMessage(string recipientEmail, string confirmationUrl);

    EmailMessage BuildPasswordResetMessage(string recipientEmail, string resetUrl);
}
