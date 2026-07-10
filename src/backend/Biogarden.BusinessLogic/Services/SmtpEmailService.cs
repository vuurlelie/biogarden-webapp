using System.Net;
using System.Net.Mail;
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
            _logger.LogInformation(
                "Email sending is disabled. Skipping email to {Recipient} with subject {Subject}.",
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
