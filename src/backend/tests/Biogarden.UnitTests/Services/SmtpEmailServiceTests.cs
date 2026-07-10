using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Biogarden.UnitTests.Services;

public sealed class SmtpEmailServiceTests
{
    [Fact]
    public async Task SendAsync_WhenEmailSendingIsDisabled_DoesNotRequireSmtpConfiguration()
    {
        var service = CreateService(new EmailOptions { Enabled = false });

        await service.SendAsync(new EmailMessage
        {
            To = "vasarlo@example.com",
            Subject = "Teszt email",
            PlainTextBody = "Ez egy teszt email."
        });
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

    private static SmtpEmailService CreateService(EmailOptions options)
    {
        return new SmtpEmailService(
            Options.Create(options),
            NullLogger<SmtpEmailService>.Instance);
    }
}
