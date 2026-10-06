using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Biogarden.WebApi.Health;

// Read-only operational signal. Keep it separate from readiness: a delivery incident
// must not remove an otherwise functional shop from traffic or restart its workers.
public sealed class OperationalHealthCheck(AppDbContext database, TimeProvider clock,
    IOptions<OperationalMonitoringOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var settings = options.Value;
        var emailCutoff = now.AddMinutes(-settings.EmailOverdueMinutes);
        var paymentCutoff = now.AddMinutes(-settings.PaymentRecoveryMinutes);
        var orderEmailIssue = await database.OrderEmails.AnyAsync(x => x.SentAtUtc == null &&
            (x.Attempts >= settings.EmailFailureAttempts || x.NextAttemptAtUtc <= emailCutoff), cancellationToken);
        var notificationIssue = await database.NotificationEmails.AnyAsync(x => x.CompletedAtUtc == null && x.ExpiresAtUtc > now &&
            (x.Attempts >= settings.EmailFailureAttempts || x.NextAttemptAtUtc <= emailCutoff), cancellationToken);
        var failedEvent = await database.PaymentProviderEvents.AnyAsync(x => x.ProcessedAtUtc == null &&
            x.ProcessingFailedAtUtc != null && x.ReceivedAtUtc <= paymentCutoff, cancellationToken);
        var ambiguousPayment = await database.PaymentAttempts.AnyAsync(x => x.CreateRequested && x.CheckoutId == null &&
            x.CreatedAtUtc <= paymentCutoff, cancellationToken);
        var pendingRefund = await database.Orders.AnyAsync(x => x.PaymentStatus == PaymentStatus.RefundPending, cancellationToken);
        return orderEmailIssue || notificationIssue || failedEvent || ambiguousPayment || pendingRefund
            ? HealthCheckResult.Unhealthy("Operational follow-up required.")
            : HealthCheckResult.Healthy();
    }
}
