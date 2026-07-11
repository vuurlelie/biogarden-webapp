using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Biogarden.UnitTests.Services;

public sealed class SmtpEmailServiceTests
{
    [Fact]
    public async Task SendAsync_WhenEmailSendingIsDisabled_WritesPreviewWithoutSmtpConfiguration()
    {
        var previewDirectory = Path.Combine(
            Path.GetTempPath(),
            "biogarden-email-tests",
            Guid.NewGuid().ToString("N"));

        var service = CreateService(new EmailOptions
        {
            Enabled = false,
            DisabledDeliveryPreviewDirectory = previewDirectory
        });

        await service.SendAsync(new EmailMessage
        {
            To = "vasarlo@example.com",
            Subject = "Teszt email",
            PlainTextBody = "Ez egy teszt email.",
            HtmlBody = "<p>Ez egy teszt email.</p>"
        });

        var latestHtmlPath = Path.Combine(previewDirectory, "latest.html");
        var latestTextPath = Path.Combine(previewDirectory, "latest.txt");

        Assert.True(File.Exists(latestHtmlPath));
        Assert.True(File.Exists(latestTextPath));
        Assert.Contains(
            "Ez egy teszt email.",
            await File.ReadAllTextAsync(latestTextPath));
    }

    [Fact]
    public async Task SendAsync_WhenEmailSendingIsEnabledWithoutSmtpHost_ThrowsConfigurationError()
    {
        var service = CreateService(new EmailOptions
        {
            Enabled = true,
            FromAddress = "info@bakonykincse.hu",
            SmtpHost = string.Empty,
            SmtpPort = 587
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAsync(new EmailMessage
            {
                To = "vasarlo@example.com",
                Subject = "Teszt email",
                PlainTextBody = "Ez egy teszt email."
            }));

        Assert.Equal(
            "Email sending is enabled, but Email:SmtpHost is not configured.",
            exception.Message);
    }

    [Fact]
    public async Task SendAsync_WhenRecipientEmailIsInvalid_ThrowsBeforeConfigurationCheck()
    {
        var service = CreateService(new EmailOptions { Enabled = false });

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendAsync(new EmailMessage
            {
                To = "hibas-email",
                Subject = "Teszt email",
                PlainTextBody = "Ez egy teszt email."
            }));

        Assert.Equal("message", exception.ParamName);
    }

    [Fact]
    public void ValidateForStartup_WhenEmailIsEnabledWithValidSmtpSettings_DoesNotThrow()
    {
        var options = new EmailOptions
        {
            Enabled = true,
            FromAddress = "info@bakonykincse.hu",
            SmtpHost = "smtp.example.com",
            SmtpPort = 587
        };

        options.ValidateForStartup();
    }

    [Fact]
    public void ValidateForStartup_WhenEmailIsDisabledWithoutPreviewDirectory_Throws()
    {
        var options = new EmailOptions
        {
            Enabled = false,
            DisabledDeliveryPreviewDirectory = string.Empty
        };

        var exception = Assert.Throws<InvalidOperationException>(
            options.ValidateForStartup);

        Assert.Equal(
            "Email sending is disabled, but Email:DisabledDeliveryPreviewDirectory is not configured.",
            exception.Message);
    }

    [Fact]
    public void ValidateForStartup_WhenAuthEmailClientBaseUrlIsInvalid_Throws()
    {
        var options = new AuthEmailOptions
        {
            ClientBaseUrl = "not-a-url"
        };

        var exception = Assert.Throws<InvalidOperationException>(
            options.ValidateForStartup);

        Assert.Equal(
            "AuthEmail:ClientBaseUrl must be an absolute HTTP or HTTPS URL.",
            exception.Message);
    }

    [Fact]
    public void ValidateForStartup_WhenBrandingContactEmailIsInvalid_Throws()
    {
        var options = new EmailBrandingOptions
        {
            ContactEmail = "hibas-email"
        };

        var exception = Assert.Throws<InvalidOperationException>(
            options.ValidateForStartup);

        Assert.Equal(
            "EmailBranding:ContactEmail must be a valid email address.",
            exception.Message);
    }

    private static SmtpEmailService CreateService(EmailOptions options)
    {
        return new SmtpEmailService(
            Options.Create(options),
            NullLogger<SmtpEmailService>.Instance);
    }
}
