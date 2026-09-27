using Biogarden.DataAccess;
using Biogarden.WebApi.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.WebApi.Filters;

// Serialize account-security mutations across API instances. Authorization is checked again
// after waiting, so two admins cannot demote/deactivate each other using stale authorization.
public class AccountSecurityMutationFilter : IAsyncActionFilter
{
    public virtual async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            await next();
            return;
        }

        var database = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var settings = new SqlConnectionStringBuilder(database.Database.GetConnectionString()) { Pooling = false };
        // A dedicated unpooled connection owns the session lock. Disposal releases it even on
        // cancellation/failure; no transaction spans SMTP or changes existing persistence semantics.
        await using var connection = new SqlConnection(settings.ConnectionString);
        var cancellationToken = context.HttpContext.RequestAborted;
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = N'Biogarden.AccountSecurity', @LockMode = N'Exclusive',
                @LockOwner = N'Session', @LockTimeout = 5000;
            SELECT @result;
            """;
        var result = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        if (result < 0)
            throw new InvalidOperationException("Másik fiókmódosítás van folyamatban. Kérjük, próbáld újra.");

        if (!await SessionAuthentication.IsCurrentAsync(
            context.HttpContext.User, context.HttpContext.RequestServices, cancellationToken))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}
