using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.DTOs.Contact;
using Biogarden.BusinessLogic.DTOs.Orders;

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

    EmailMessage BuildOrderConfirmationMessage(string recipientEmail, OrderDto order);

    EmailMessage BuildOrderBusinessNotificationMessage(string recipientEmail, OrderDto order);

    EmailMessage BuildOrderStatusChangedMessage(string recipientEmail, OrderDto order);

    EmailMessage BuildOrderWithdrawalBusinessNotificationMessage(
        string recipientEmail,
        OrderDto order,
        OrderWithdrawalRequestDto request);

    EmailMessage BuildOrderWithdrawalCustomerConfirmationMessage(
        string recipientEmail,
        OrderDto order,
        OrderWithdrawalRequestDto request);
}
