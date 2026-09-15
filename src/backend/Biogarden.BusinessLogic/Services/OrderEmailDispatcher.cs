using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.DataAccess.Abstractions;
using Microsoft.Extensions.Logging;

namespace Biogarden.BusinessLogic.Services;

public sealed class OrderEmailDispatcher(
    IOrderEmailRepository repository,
    IEmailService emailService,
    TimeProvider timeProvider,
    ILogger<OrderEmailDispatcher> logger)
{
    public async Task<bool> DispatchNextAsync(CancellationToken token)
    {
        var email = await repository.TryClaimAsync(timeProvider.GetUtcNow().UtcDateTime, token);
        if (email is null) return false;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await emailService.SendAsync(new EmailMessage
            {
                To = email.Recipient,
                Subject = email.Subject,
                HtmlBody = email.HtmlBody,
                PlainTextBody = email.PlainTextBody
            }, timeout.Token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The lease expires after shutdown so a new worker can retry.
            throw;
        }
        catch (Exception exception)
        {
            // Avoid logging message bodies, recipient addresses, or SMTP exception details.
            logger.LogWarning("Order email {EmailId} attempt {Attempt} failed ({ErrorType}); retry scheduled.",
                email.Id, email.Attempts, exception.GetType().Name);
            var delay = TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Min(email.Attempts - 1, 6))));
            await repository.RetryAsync(email.Id, email.LeaseId!.Value,
                timeProvider.GetUtcNow().UtcDateTime.Add(delay), token);
            return true;
        }

        // A database failure here leaves the lease to expire; delivery is at least once.
        await repository.CompleteAsync(email.Id, email.LeaseId!.Value,
            timeProvider.GetUtcNow().UtcDateTime, token);
        return true;
    }
}
