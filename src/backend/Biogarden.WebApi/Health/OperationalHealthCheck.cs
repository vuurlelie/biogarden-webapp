using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Biogarden.WebApi.Health;

// Read-only operational signal. Keep it separate from readiness: a delivery incident
// must not remove an otherwise functional shop from traffic or restart its workers.
public sealed class OperationalHealthCheck(AppDbContext database, TimeProvider clock, IOptions<OperationalMonitoringOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var settings = options.Value;
        var emailCutoff = now.AddMinutes(-settings.EmailOverdueMinutes);
        var paymentCutoff = now.AddMinutes(-settings.PaymentRecoveryMinutes);
        var orderEmailIssue = await database.OrderEmails.AnyAsync(
            orderEmail => orderEmail.SentAtUtc == null &&
                (orderEmail.Attempts >= settings.EmailFailureAttempts || orderEmail.NextAttemptAtUtc <= emailCutoff),
            cancellationToken
        );
        var notificationIssue = await database.NotificationEmails.AnyAsync(
            notificationEmail => notificationEmail.CompletedAtUtc == null && notificationEmail.ExpiresAtUtc > now &&
                (notificationEmail.Attempts >= settings.EmailFailureAttempts || notificationEmail.NextAttemptAtUtc <= emailCutoff),
            cancellationToken
        );
        var failedEvent = await database.PaymentProviderEvents.AnyAsync(
            paymentProviderEvent => paymentProviderEvent.ProcessedAtUtc == null &&
                paymentProviderEvent.ProcessingFailedAtUtc != null && paymentProviderEvent.ReceivedAtUtc <= paymentCutoff,
            cancellationToken
        );
        var ambiguousPayment = await database.PaymentAttempts.AnyAsync(
            paymentAttempt => paymentAttempt.CreateRequested && paymentAttempt.CheckoutId == null &&
                paymentAttempt.CreatedAtUtc <= paymentCutoff,
            cancellationToken
        );
        var pendingRefund = await database.Orders.AnyAsync(order => order.PaymentStatus == PaymentStatus.RefundPending, cancellationToken);
        return orderEmailIssue || notificationIssue || failedEvent || ambiguousPayment || pendingRefund
            ? HealthCheckResult.Unhealthy("Operational follow-up required.")
            : HealthCheckResult.Healthy();
    }
}
