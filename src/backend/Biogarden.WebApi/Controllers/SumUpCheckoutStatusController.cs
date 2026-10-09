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
    private const int MaxPayloadBytes = 16 * 1024;
    private readonly ISumUpCheckoutStatusService _checkoutStatusService;

    public SumUpCheckoutStatusController(ISumUpCheckoutStatusService checkoutStatusService)
    {
        _checkoutStatusService = checkoutStatusService;
    }

    [HttpPost]
    [RequestSizeLimit(MaxPayloadBytes)]
    [EnableRateLimiting(RateLimitPolicyNames.Payment)]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        // Also bound chunked requests and test hosts which do not enforce RequestSizeLimit.
        using var body = new MemoryStream();
        var buffer = new byte[4096];
        int count;
        while ((count = await Request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (body.Length + count > MaxPayloadBytes)
                return StatusCode(StatusCodes.Status413PayloadTooLarge);
            body.Write(buffer, 0, count);
        }
        var payload = System.Text.Encoding.UTF8.GetString(body.ToArray());

        await _checkoutStatusService.HandleAsync(payload, cancellationToken);

        return Ok();
    }
}
