using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class OrderEmailDispatcherTests
{
    private readonly Mock<IOrderEmailRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IEmailService> _sender = new(MockBehavior.Strict);
    private readonly DateTime _now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FailedEmail_IsRetriedAndDoesNotBlockNextEmail()
    {
        var failed = Message();
        var business = Message();
        business.Recipient = "business@example.com";
        _repository.SetupSequence(orderEmailRepository => orderEmailRepository.TryClaimAsync(_now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failed).ReturnsAsync(business).ReturnsAsync((OrderEmail?)null);
        _sender.Setup(emailService => emailService.SendAsync(It.Is<EmailMessage>(emailMessage => emailMessage.To == failed.Recipient), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Mail.SmtpException("Unavailable"));
        _sender.Setup(emailService => emailService.SendAsync(It.Is<EmailMessage>(emailMessage => emailMessage.To == business.Recipient), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repository.Setup(orderEmailRepository => orderEmailRepository.RetryAsync(failed.Id, failed.LeaseId!.Value, _now.AddMinutes(1), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repository.Setup(orderEmailRepository => orderEmailRepository.CompleteAsync(business.Id, business.LeaseId!.Value, _now, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dispatcher = Dispatcher();
        Assert.True(await dispatcher.DispatchNextAsync(default));
        Assert.True(await dispatcher.DispatchNextAsync(default));
        Assert.False(await dispatcher.DispatchNextAsync(default));
        _repository.VerifyAll();
        _repository.Verify(
            orderEmailRepository => orderEmailRepository.CompleteAsync(failed.Id, It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task SuccessfulRetry_MarksEmailSent()
    {
        var email = Message();
        email.Attempts = 2;
        _repository.Setup(orderEmailRepository => orderEmailRepository.TryClaimAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync(email);
        _sender.Setup(emailService => emailService.SendAsync(
            It.Is<EmailMessage>(emailMessage => emailMessage.To == email.Recipient && emailMessage.Subject == email.Subject && emailMessage.HtmlBody == email.HtmlBody),
            It.IsAny<CancellationToken>()
        ))
            .Returns(Task.CompletedTask);
        _repository.Setup(orderEmailRepository => orderEmailRepository.CompleteAsync(email.Id, email.LeaseId!.Value, _now, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Assert.True(await Dispatcher().DispatchNextAsync(default));
        _repository.VerifyAll();
        _sender.VerifyAll();
    }

    [Fact]
    public async Task Shutdown_DoesNotMarkEmailSentOrDiscardIt()
    {
        using var cancellation = new CancellationTokenSource();
        var email = Message();
        _repository.Setup(orderEmailRepository => orderEmailRepository.TryClaimAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync(email);
        _sender.Setup(emailService => emailService.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback(() => cancellation.Cancel())
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => Dispatcher().DispatchNextAsync(cancellation.Token));
        _repository.Verify(orderEmailRepository => orderEmailRepository.TryClaimAsync(_now, cancellation.Token), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    private OrderEmailDispatcher Dispatcher() => new(_repository.Object, _sender.Object, new FixedTimeProvider(_now), NullLogger<OrderEmailDispatcher>.Instance);

    [Theory]
    [InlineData(2, 2)]
    [InlineData(7, 60)]
    [InlineData(100, 60)]
    public async Task RepeatedFailures_BackOffWithOneHourMaximum(int attempts, int delayMinutes)
    {
        var email = Message();
        email.Attempts = attempts;
        _repository.Setup(orderEmailRepository => orderEmailRepository.TryClaimAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync(email);
        _sender.Setup(emailService => emailService.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());
        _repository.Setup(orderEmailRepository => orderEmailRepository.RetryAsync(email.Id, email.LeaseId!.Value, _now.AddMinutes(delayMinutes), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Assert.True(await Dispatcher().DispatchNextAsync(default));
        _repository.VerifyAll();
    }

    [Fact]
    public async Task CompletionSaveFailure_PropagatesWithoutDiscardingTheLease()
    {
        var email = Message();
        _repository.Setup(orderEmailRepository => orderEmailRepository.TryClaimAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync(email);
        _sender.Setup(emailService => emailService.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repository.Setup(orderEmailRepository => orderEmailRepository.CompleteAsync(email.Id, email.LeaseId!.Value, _now, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Dispatcher().DispatchNextAsync(default));
        _repository.VerifyAll();
        _repository.VerifyNoOtherCalls();
    }

    private static OrderEmail Message() => new()
    {
        Id = Guid.NewGuid(),
        LeaseId = Guid.NewGuid(),
        Attempts = 1,
        Recipient = "customer@example.com",
        Subject = "Order received",
        HtmlBody = "<p>Thank you</p>"
    };

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
