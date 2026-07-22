using System.Net.Mail;
using Microsoft.Extensions.Logging;

namespace Biogarden.BusinessLogic.Options;

public sealed class ErrorAlertOptions
{
    public const string SectionName = "ErrorAlerts";

    public bool Enabled { get; init; }
    public string RecipientEmail { get; init; } = string.Empty;
    public string ApplicationName { get; init; } = "Biogarden";
    public LogLevel MinimumLevel { get; init; } = LogLevel.Error;
    public int ThrottleWindowMinutes { get; init; } = 30;

    public void ValidateForStartup()
    {
        if (ThrottleWindowMinutes <= 0)
        {
            throw new InvalidOperationException(
                "ErrorAlerts:ThrottleWindowMinutes must be greater than zero.");
        }

        if (!Enabled)
        {
            return;
        }

        if (!MailAddress.TryCreate(RecipientEmail, out _))
        {
            throw new InvalidOperationException(
                "Error alerts are enabled, but ErrorAlerts:RecipientEmail is not a valid email address.");
        }
    }
}
