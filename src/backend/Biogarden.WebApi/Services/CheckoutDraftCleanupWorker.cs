using Biogarden.BusinessLogic.Services;

namespace Biogarden.WebApi.Services;

public sealed class CheckoutDraftCleanupWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<CheckoutDraftCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("CheckoutDrafts:CleanupEnabled", true)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var count = await scope.ServiceProvider.GetRequiredService<CheckoutDraftService>().PurgeExpiredAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Removed personal data from {Count} expired checkout drafts.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                // Draft payloads and database parameter values must never reach application logs.
                logger.LogError("Checkout draft cleanup failed ({ErrorType}).", exception.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
