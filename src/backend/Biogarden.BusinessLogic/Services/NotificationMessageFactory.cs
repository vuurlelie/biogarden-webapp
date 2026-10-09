using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Biogarden.BusinessLogic.Services;

public sealed class NotificationMessageFactory(UserManager<ApplicationUser> users, IEmailTemplateService templates, IOptions<AuthEmailOptions> options)
{
    public async Task<EmailMessage?> BuildAsync(NotificationEmail email)
    {
        if (email.Kind is not (AccountNotificationQueue.Confirmation or AccountNotificationQueue.PasswordReset))
            return new EmailMessage { To = email.Recipient, Subject = email.Subject, HtmlBody = email.HtmlBody, PlainTextBody = email.PlainTextBody };
        if (email.UserId is null)
            return null;
        var user = await users.FindByIdAsync(email.UserId.Value.ToString());

        if (user is null || user.RecordStatus != RecordStatus.Active || user.DeactivatedAtUtc is not null || user.AnonymizedAtUtc is not null ||
            (user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow) || string.IsNullOrEmpty(email.SecurityStamp) ||
            user.SecurityStamp != email.SecurityStamp || !string.Equals(user.Email, email.Recipient, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var confirmation = email.Kind == AccountNotificationQueue.Confirmation;
        if (confirmation == user.EmailConfirmed)
            return null;
        var token = confirmation ? await users.GenerateEmailConfirmationTokenAsync(user) : await users.GeneratePasswordResetTokenAsync(user);
        var settings = options.Value;
        var path = confirmation ? settings.ConfirmEmailPath : settings.ResetPasswordPath;
        var url = $"{settings.ClientBaseUrl.TrimEnd('/')}{path}?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        return confirmation ? templates.BuildEmailConfirmationMessage(email.Recipient, url) : templates.BuildPasswordResetMessage(email.Recipient, url);
    }
}
