using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.DataAccess.Abstractions;
using Microsoft.Extensions.Logging;

namespace Biogarden.BusinessLogic.Services;

public sealed class NotificationEmailDispatcher(INotificationEmailRepository repository, IEmailService sender,
    TimeProvider timeProvider, ILogger<NotificationEmailDispatcher> logger)
{
    public async Task<bool> DispatchNextAsync(CancellationToken token)
    {
        await repository.ExpireAsync(timeProvider.GetUtcNow().UtcDateTime, token);
        var email = await repository.TryClaimAsync(timeProvider.GetUtcNow().UtcDateTime, token);
        if (email is null) return false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await sender.SendAsync(new EmailMessage
            {
                To = email.Recipient,
                Subject = email.Subject,
                HtmlBody = email.HtmlBody,
                PlainTextBody = email.PlainTextBody
            }, timeout.Token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning("Notification {Id} attempt {Attempt} failed ({ErrorType}); retry scheduled.", email.Id, email.Attempts, exception.GetType().Name);
            var delay = TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Min(email.Attempts - 1, 6))));
            await repository.RetryAsync(email.Id, email.LeaseId!.Value, timeProvider.GetUtcNow().UtcDateTime.Add(delay), token);
            return true;
        }
        await repository.CompleteAsync(email.Id, email.LeaseId!.Value, timeProvider.GetUtcNow().UtcDateTime, "sent", token);
        return true;
    }
}
