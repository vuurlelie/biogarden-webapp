using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;

namespace Biogarden.BusinessLogic.Services;

public sealed class AccountNotificationQueue(IAppDbContext database, TimeProvider time) : IAccountNotificationQueue
{
    public const string Confirmation = "email-confirmation";
    public const string PasswordReset = "password-reset";

    public async Task QueueAuthenticationAsync(ApplicationUser user, string kind, CancellationToken token = default)
    {
        if (kind is not (Confirmation or PasswordReset)) throw new ArgumentOutOfRangeException(nameof(kind));
        var now = time.GetUtcNow().UtcDateTime;
        database.NotificationEmails.Add(new NotificationEmail
        {
            EventId = Guid.NewGuid(),
            Kind = kind,
            UserId = user.Id,
            SecurityStamp = user.SecurityStamp,
            Recipient = user.Email ?? throw new InvalidOperationException("A felhasználó email címe hiányzik."),
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            ExpiresAtUtc = kind == PasswordReset ? now.AddHours(1) : now.AddDays(1)
        });
        await database.SaveChangesAsync(token);
    }

    public async Task QueueMessageAsync(EmailMessage message, string kind, CancellationToken token = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        database.NotificationEmails.Add(new NotificationEmail
        {
            EventId = Guid.NewGuid(),
            Kind = kind,
            Recipient = message.To,
            Subject = message.Subject,
            HtmlBody = message.HtmlBody,
            PlainTextBody = message.PlainTextBody,
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            ExpiresAtUtc = now.AddDays(7)
        });
        await database.SaveChangesAsync(token);
    }
}
