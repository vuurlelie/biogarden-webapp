using Biogarden.BusinessLogic.Abstractions;
using Biogarden.WebApi.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/sumup/checkout-status")]
public sealed class SumUpCheckoutStatusController : ControllerBase
{
    private readonly ISumUpCheckoutStatusService _checkoutStatusService;

    public SumUpCheckoutStatusController(ISumUpCheckoutStatusService checkoutStatusService)
    {
        _checkoutStatusService = checkoutStatusService;
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicyNames.Payment)]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync(cancellationToken);

        await _checkoutStatusService.HandleAsync(
            payload,
            cancellationToken);

        return Ok();
    }
}
