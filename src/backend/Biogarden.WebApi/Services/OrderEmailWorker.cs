using Biogarden.BusinessLogic.Services;

namespace Biogarden.WebApi.Services;

public sealed class OrderEmailWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<OrderEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("OrderEmailOutbox:Enabled", true)) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OrderEmailDispatcher>();
                if (await dispatcher.DispatchNextAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("Order email worker failed ({ErrorType}); polling will resume.", exception.GetType().Name);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
