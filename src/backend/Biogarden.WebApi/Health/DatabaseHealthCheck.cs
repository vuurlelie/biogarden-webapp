using Biogarden.DataAccess;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Biogarden.WebApi.Health;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;

    public DatabaseHealthCheck(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("Database connection is available.")
                : HealthCheckResult.Unhealthy("Database connection is not available.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database health check failed.", exception);
        }
    }
}
