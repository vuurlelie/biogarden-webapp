using System.Text;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Alerting;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Options;
using Microsoft.Extensions.Options;

namespace Biogarden.BusinessLogic.Services;

public sealed class ErrorAlertService : IErrorAlertService
{
    private readonly ErrorAlertOptions _options;
    private readonly IEmailService _emailService;
    private readonly ErrorAlertThrottleStore _throttleStore;
    private readonly TimeProvider _timeProvider;

    public ErrorAlertService(
        IOptions<ErrorAlertOptions> options,
        IEmailService emailService,
        ErrorAlertThrottleStore throttleStore,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _emailService = emailService;
        _throttleStore = throttleStore;
        _timeProvider = timeProvider;
    }

    public async Task NotifyAsync(
        ErrorAlert alert,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || alert.Level < _options.MinimumLevel)
        {
            return;
        }

        var throttleKey = $"{alert.Level}:{NormalizeKey(alert.AlertKey)}";
        var throttleWindow = TimeSpan.FromMinutes(_options.ThrottleWindowMinutes);

        if (!_throttleStore.TryAcquire(
            throttleKey,
            _timeProvider.GetUtcNow(),
            throttleWindow))
        {
            return;
        }

        await _emailService.SendAsync(
            BuildEmailMessage(alert),
            cancellationToken);
    }

    private EmailMessage BuildEmailMessage(ErrorAlert alert)
    {
        return new EmailMessage
        {
            To = _options.RecipientEmail,
            Subject = $"[{_options.ApplicationName}] {alert.Level}: {alert.Summary}",
            PlainTextBody = BuildPlainTextBody(alert)
        };
    }

    private string BuildPlainTextBody(ErrorAlert alert)
    {
        var body = new StringBuilder();

        body.AppendLine($"{_options.ApplicationName} error alert");
        body.AppendLine();
        body.AppendLine($"Severity: {alert.Level}");
        body.AppendLine($"Source: {alert.Source}");
        body.AppendLine($"Alert key: {alert.AlertKey}");
        body.AppendLine($"Summary: {alert.Summary}");

        if (!string.IsNullOrWhiteSpace(alert.Detail))
        {
            body.AppendLine();
            body.AppendLine("Detail:");
            body.AppendLine(alert.Detail);
        }

        if (alert.Exception is not null)
        {
            body.AppendLine();
            body.AppendLine("Exception:");
            body.AppendLine(alert.Exception.ToString());
        }

        body.AppendLine();
        body.AppendLine($"Repeated alerts with the same key are muted for {_options.ThrottleWindowMinutes} minutes.");

        return body.ToString();
    }

    private static string NormalizeKey(string key)
    {
        return key.Trim().ToUpperInvariant();
    }
}
