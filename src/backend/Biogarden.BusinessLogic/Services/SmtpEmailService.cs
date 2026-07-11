using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Biogarden.BusinessLogic.Services;

public sealed class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(
        IOptions<EmailOptions> options,
        ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ValidateMessage(message);

        if (!_options.Enabled)
        {
            await WriteDisabledDeliveryPreviewAsync(message, cancellationToken);

            _logger.LogInformation(
                "Email sending is disabled. Saved preview for email to {Recipient} with subject {Subject}.",
                message.To,
                message.Subject);

            return;
        }

        _options.ValidateForSending();

        using var mailMessage = BuildMailMessage(message);
        using var smtpClient = BuildSmtpClient();

        await smtpClient.SendMailAsync(mailMessage, cancellationToken);
    }

    private MailMessage BuildMailMessage(EmailMessage message)
    {
        var sender = string.IsNullOrWhiteSpace(_options.FromName)
            ? new MailAddress(_options.FromAddress)
            : new MailAddress(_options.FromAddress, _options.FromName);

        var mailMessage = new MailMessage
        {
            From = sender,
            Subject = message.Subject,
            Body = message.HtmlBody ?? message.PlainTextBody ?? string.Empty,
            IsBodyHtml = !string.IsNullOrWhiteSpace(message.HtmlBody)
        };

        mailMessage.To.Add(new MailAddress(message.To));

        if (!string.IsNullOrWhiteSpace(message.PlainTextBody)
            && !string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            mailMessage.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    message.PlainTextBody,
                    contentEncoding: null,
                    "text/plain"));

            mailMessage.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    message.HtmlBody,
                    contentEncoding: null,
                    "text/html"));
        }

        return mailMessage;
    }

    private SmtpClient BuildSmtpClient()
    {
        var smtpClient = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            smtpClient.Credentials = new NetworkCredential(
                _options.Username,
                _options.Password);
        }

        return smtpClient;
    }

    private async Task WriteDisabledDeliveryPreviewAsync(
        EmailMessage message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.DisabledDeliveryPreviewDirectory))
        {
            return;
        }

        var previewDirectory = Path.GetFullPath(_options.DisabledDeliveryPreviewDirectory);
        Directory.CreateDirectory(previewDirectory);

        var safeSubject = CreateSafeFileName(message.Subject);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var baseFileName = $"{timestamp}-{safeSubject}";

        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            var htmlPath = Path.Combine(previewDirectory, $"{baseFileName}.html");
            await File.WriteAllTextAsync(
                htmlPath,
                message.HtmlBody,
                Encoding.UTF8,
                cancellationToken);

            var latestHtmlPath = Path.Combine(previewDirectory, "latest.html");
            await File.WriteAllTextAsync(
                latestHtmlPath,
                message.HtmlBody,
                Encoding.UTF8,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(message.PlainTextBody))
        {
            var textPath = Path.Combine(previewDirectory, $"{baseFileName}.txt");
            await File.WriteAllTextAsync(
                textPath,
                message.PlainTextBody,
                Encoding.UTF8,
                cancellationToken);

            var latestTextPath = Path.Combine(previewDirectory, "latest.txt");
            await File.WriteAllTextAsync(
                latestTextPath,
                message.PlainTextBody,
                Encoding.UTF8,
                cancellationToken);
        }
    }

    private static string CreateSafeFileName(string value)
    {
        var safeValue = Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-");
        return safeValue.Trim('-');
    }

    private static void ValidateMessage(EmailMessage message)
    {
        if (!MailAddress.TryCreate(message.To, out _))
        {
            throw new ArgumentException(
                "A címzett email címe nem érvényes.",
                nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.Subject))
        {
            throw new ArgumentException(
                "Az email tárgya kötelező.",
                nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.PlainTextBody)
            && string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            throw new ArgumentException(
                "Az email tartalma kötelező.",
                nameof(message));
        }
    }
}
