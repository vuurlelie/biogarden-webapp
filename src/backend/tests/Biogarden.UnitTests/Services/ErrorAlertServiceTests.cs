using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Alerting;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class ErrorAlertServiceTests
{
    private readonly Mock<IEmailService> _emailService = new(MockBehavior.Strict);
    private readonly FakeTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task NotifyAsync_WhenAlertsAreDisabled_DoesNotSendEmail()
    {
        var service = CreateService(new ErrorAlertOptions
        {
            Enabled = false,
            RecipientEmail = "owner@example.com"
        });

        await service.NotifyAsync(CreateAlert());

        _emailService.Verify(service => service.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_WhenAlertIsBelowMinimumLevel_DoesNotSendEmail()
    {
        var service = CreateService(new ErrorAlertOptions
        {
            Enabled = true,
            RecipientEmail = "owner@example.com",
            MinimumLevel = LogLevel.Critical
        });

        await service.NotifyAsync(CreateAlert(level: LogLevel.Error));

        _emailService.Verify(service => service.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_WhenAlertsAreEnabled_SendsEmailWithAlertDetails()
    {
        EmailMessage? sentMessage = null;
        _emailService
            .Setup(service => service.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>(
                (message, _) => sentMessage = message)
            .Returns(Task.CompletedTask);

        var service = CreateService(new ErrorAlertOptions
        {
            Enabled = true,
            RecipientEmail = "owner@example.com",
            ApplicationName = "Biogarden Test",
            MinimumLevel = LogLevel.Warning
        });

        await service.NotifyAsync(CreateAlert(level: LogLevel.Error, detail: "The checkout request failed.", exception: new InvalidOperationException("SumUp error.")));

        Assert.NotNull(sentMessage);
        Assert.Equal("owner@example.com", sentMessage.To);
        Assert.Equal("[Biogarden Test] Error: Payment failed", sentMessage.Subject);
        Assert.Contains("Severity: Error", sentMessage.PlainTextBody);
        Assert.Contains("Source: Checkout", sentMessage.PlainTextBody);
        Assert.Contains("Alert key: sumup.checkout.create", sentMessage.PlainTextBody);
        Assert.Contains("The checkout request failed.", sentMessage.PlainTextBody);
        Assert.Contains("SumUp error.", sentMessage.PlainTextBody);
    }

    [Fact]
    public async Task NotifyAsync_WhenAlertKeyRepeats_ThrottlesDuplicateEmails()
    {
        var sendCount = 0;
        _emailService
            .Setup(service => service.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback(() => sendCount++)
            .Returns(Task.CompletedTask);

        var service = CreateService(new ErrorAlertOptions
        {
            Enabled = true,
            RecipientEmail = "owner@example.com",
            ThrottleWindowMinutes = 30
        });

        await service.NotifyAsync(CreateAlert());
        await service.NotifyAsync(CreateAlert());

        _timeProvider.Advance(TimeSpan.FromMinutes(31));
        await service.NotifyAsync(CreateAlert());

        Assert.Equal(2, sendCount);
    }

    [Fact]
    public async Task FailedSend_UsesShortCooldown_ThenAllowsAnotherAttempt()
    {
        _emailService.SetupSequence(emailService => emailService.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("SMTP unavailable")).Returns(Task.CompletedTask);
        var service = CreateService(new ErrorAlertOptions { Enabled = true, FailureRetrySeconds = 60 });
        await Assert.ThrowsAsync<IOException>(() => service.NotifyAsync(CreateAlert()));
        await service.NotifyAsync(CreateAlert());
        _emailService.Verify(emailService => emailService.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _timeProvider.Advance(TimeSpan.FromSeconds(61));
        await service.NotifyAsync(CreateAlert());
        _emailService.Verify(emailService => emailService.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task InFlightSend_IsDeduplicated_AndBoundedEvenIfSenderIgnoresCancellation()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _emailService.Setup(emailService => emailService.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).Returns(pending.Task);
        var service = CreateService(new ErrorAlertOptions { Enabled = true, SendTimeoutSeconds = 1 });
        var first = service.NotifyAsync(CreateAlert());
        await service.NotifyAsync(CreateAlert());
        try { await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(TimeSpan.FromSeconds(5))); }
        finally { pending.TrySetResult(); }
        _emailService.Verify(emailService => emailService.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(31, 60)]
    [InlineData(5, 0)]
    public void InvalidDeliveryLimits_AreRejectedAtStartup(int timeout, int retry)
    {
        Assert.Throws<InvalidOperationException>(() => new ErrorAlertOptions
        { SendTimeoutSeconds = timeout, FailureRetrySeconds = retry }.ValidateForStartup());
    }

    private ErrorAlertService CreateService(ErrorAlertOptions options)
    {
        return new ErrorAlertService(Options.Create(options), _emailService.Object, new ErrorAlertThrottleStore(), _timeProvider);
    }

    private static ErrorAlert CreateAlert(LogLevel level = LogLevel.Error, string? detail = null, Exception? exception = null)
    {
        return new ErrorAlert
        {
            AlertKey = "sumup.checkout.create",
            Source = "Checkout",
            Summary = "Payment failed",
            Level = level,
            Detail = detail,
            Exception = exception
        };
    }
}

internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    public void Advance(TimeSpan value)
    {
        _utcNow = _utcNow.Add(value);
    }
}
