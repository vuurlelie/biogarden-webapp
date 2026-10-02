using Biogarden.DataAccess.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.BusinessLogic.Services;

internal static class NotificationTransaction
{
    public static async Task<T> RunAsync<T>(IAppDbContext database, Func<Task<T>> operation, CancellationToken token = default)
    {
        if (database is not DbContext context || context.Database.CurrentTransaction is not null)
            return await operation();
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Reload state on a transient retry; never reuse objects from a rolled-back attempt.
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(token);
            try
            {
                var result = await operation();
                await transaction.CommitAsync(token);
                return result;
            }
            catch
            {
                context.ChangeTracker.Clear();
                throw;
            }
        });
    }
}
