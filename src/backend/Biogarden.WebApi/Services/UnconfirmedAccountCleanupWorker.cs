using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Microsoft.Extensions.Options;

namespace Biogarden.WebApi.Services;

public sealed class UnconfirmedAccountCleanupWorker(IServiceScopeFactory scopes, IOptions<UnconfirmedAccountCleanupOptions> options, TimeProvider timeProvider, ILogger<UnconfirmedAccountCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
            return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<UnconfirmedAccountCleanupService>().RunBatchAsync(stoppingToken);
                logger.LogInformation(
                    "Expired account cleanup completed: {DeletedCount} deleted, batch limit {BatchSize}.",
                    count,
                    settings.BatchSize
                );
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                // Do not expose account identifiers, personal data or database parameters in logs.
                logger.LogError("Expired account cleanup failed ({ErrorType}); it will retry on the next interval.", exception.GetType().Name);
            }
            try
            {
                // One batch per interval: a backlog must not turn into an unbounded deletion loop.
                await Task.Delay(TimeSpan.FromMinutes(settings.IntervalMinutes), timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
