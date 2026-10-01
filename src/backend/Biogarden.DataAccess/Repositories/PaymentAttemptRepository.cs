using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Repositories;

public sealed class PaymentAttemptRepository(IAppDbContext context) : IPaymentAttemptRepository
{
    public Task<PaymentAttempt?> GetLatestAsync(Guid orderId, CancellationToken token = default) => context.PaymentAttempts
        .Where(x => x.OrderId == orderId).OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).FirstOrDefaultAsync(token);
    public Task<PaymentAttempt?> GetByReferenceAsync(string reference, CancellationToken token = default)
        => context.PaymentAttempts.SingleOrDefaultAsync(x => x.Reference == reference, token);
    public Task<PaymentAttempt?> GetByCheckoutAsync(string checkoutId, CancellationToken token = default)
        => context.PaymentAttempts.SingleOrDefaultAsync(x => x.CheckoutId == checkoutId, token);
    public void Add(PaymentAttempt attempt) => context.PaymentAttempts.Add(attempt);

    public async Task<Guid?> FindOrderIdAsync(string checkoutId, string? reference, CancellationToken token = default)
    {
        var attemptOrder = await context.PaymentAttempts.Where(x => x.CheckoutId == checkoutId ||
                (reference != null && x.Reference == reference))
            .Select(x => (Guid?)x.OrderId).SingleOrDefaultAsync(token);
        return attemptOrder ?? await context.Orders.Where(x => x.SumUpCheckoutId == checkoutId)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(token);
    }

    public async Task<IAsyncDisposable> AcquireLockAsync(Guid orderId, CancellationToken token = default)
    {
        var settings = new SqlConnectionStringBuilder(((DbContext)context).Database.GetConnectionString()) { Pooling = false };
        var connection = new SqlConnection(settings.ConnectionString);
        try
        {
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource = @resource,
                    @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 5000;
                SELECT @result;
                """;
            command.Parameters.AddWithValue("@resource", $"Biogarden.Payment:{orderId:N}");
            if ((int)(await command.ExecuteScalarAsync(token))! < 0)
                throw new InvalidOperationException("A fizetés ellenőrzése folyamatban van. Próbáld újra később.");
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }
}
