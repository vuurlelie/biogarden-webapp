using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class CartCleanupServiceTests
{
    [Fact]
    public async Task Batch_UsesRetentionCutoffBoundAndCancellationWithoutLoadingOrSavingGraphs()
    {
        var now = new DateTimeOffset(2040, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var repository = new Mock<ICartRepository>(MockBehavior.Strict);
        var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        work.SetupGet(item => item.CartRepository).Returns(repository.Object);
        repository.Setup(item => item.DeleteExpiredBatchAsync(now.UtcDateTime.AddDays(-14), 25, cancellation.Token)).ReturnsAsync(25);
        var service = new CartCleanupService(work.Object, new Clock(now),
            Options.Create(new CartCleanupOptions { RetentionDays = 14, BatchSize = 25 }));

        Assert.Equal(25, await service.RunBatchAsync(cancellation.Token));
        repository.VerifyAll();
        repository.VerifyNoOtherCalls();
        work.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisabledCleanup_DoesNotAccessDatabase()
    {
        var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var service = new CartCleanupService(work.Object, TimeProvider.System,
            Options.Create(new CartCleanupOptions { Enabled = false }));
        Assert.Equal(0, await service.RunBatchAsync());
        work.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 100, 60)]
    [InlineData(366, 100, 60)]
    [InlineData(7, 0, 60)]
    [InlineData(7, 1001, 60)]
    [InlineData(7, 100, 0)]
    [InlineData(7, 100, 1441)]
    public void InvalidSettings_AreRejected(int days, int size, int interval)
        => Assert.Throws<InvalidOperationException>(() => new CartCleanupOptions
        { RetentionDays = days, BatchSize = size, IntervalMinutes = interval }.ValidateForStartup());

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
