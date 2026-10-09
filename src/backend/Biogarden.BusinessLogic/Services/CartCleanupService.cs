using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess.Abstractions;
using Microsoft.Extensions.Options;

namespace Biogarden.BusinessLogic.Services;

public sealed class CartCleanupService(IUnitOfWork unitOfWork, TimeProvider timeProvider, IOptions<CartCleanupOptions> options)
{
    public Task<int> RunBatchAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        settings.ValidateForStartup();
        if (!settings.Enabled)
            return Task.FromResult(0);
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-settings.RetentionDays);
        return unitOfWork.CartRepository.DeleteExpiredBatchAsync(cutoff, settings.BatchSize, cancellationToken);
    }
}
