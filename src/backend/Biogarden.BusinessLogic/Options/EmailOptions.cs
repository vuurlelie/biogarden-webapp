using System.Net.Mail;

namespace Biogarden.BusinessLogic.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; init; }
    public string FromName { get; init; } = "Bakony Kincse Biokertészet";
    public string FromAddress { get; init; } = string.Empty;
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool UseSsl { get; init; } = true;

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(FromAddress)
        && !string.IsNullOrWhiteSpace(SmtpHost)
        && SmtpPort > 0;

    public void ValidateForSending()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SmtpHost))
        {
            throw new InvalidOperationException(
                "Email sending is enabled, but Email:SmtpHost is not configured.");
        }

        if (SmtpPort <= 0)
        {
            throw new InvalidOperationException(
                "Email sending is enabled, but Email:SmtpPort is invalid.");
        }

        if (!MailAddress.TryCreate(FromAddress, out _))
        {
            throw new InvalidOperationException(
                "Email sending is enabled, but Email:FromAddress is not a valid email address.");
        }
    }
}
