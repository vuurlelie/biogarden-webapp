using Biogarden.WebApi.Authentication;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Biogarden.WebApi.Filters;

public sealed class BrowserCookieRequestFilter : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var expected = context.HttpContext.Request.Headers["X-Browser-Session"];
        var actual = context.HttpContext.User.FindFirstValue(BrowserAuthentication.SessionClaim) ?? "guest";
        if (expected.Count > 0 && (expected.Count != 1 || expected != actual))
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }
        if (context.HttpContext.User.HasClaim(x => x.Type == BrowserAuthentication.SessionClaim))
            return new BrowserSessionRequestAttribute().OnActionExecutionAsync(context, next);
        return next();
    }
}
