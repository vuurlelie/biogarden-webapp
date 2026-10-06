using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Biogarden.WebApi.Filters;

// Cookie-owned requests require a non-simple header. Cross-origin browser scripts must
// pass CORS preflight; normal HTML forms cannot supply the header. Provider callbacks
// deliberately do not use this filter.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BrowserSessionRequestAttribute : ActionFilterAttribute
{
    public bool ProtectReads { get; set; }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (http.Request.Headers.ContainsKey("Authorization") && http.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        if (!ProtectReads && (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)))
        {
            await next();
            return;
        }
        if (http.Request.Headers["X-Checkout-Request"] != "1")
        {
            context.Result = new BadRequestObjectResult(new { message = "Érvénytelen böngészőkérés." });
            return;
        }
        if (http.Request.Headers.TryGetValue("Origin", out var origin))
        {
            var provider = http.RequestServices.GetRequiredService<ICorsPolicyProvider>();
            var policy = await provider.GetPolicyAsync(http, "FrontendDev");
            if (origin.Count != 1 || policy is null || !policy.IsOriginAllowed(origin.ToString()))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                return;
            }
        }
        await next();
    }
}
