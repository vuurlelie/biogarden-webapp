using Biogarden.BusinessLogic.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/stripe/webhook")]
public sealed class StripeWebhookController : ControllerBase
{
    private readonly IStripeWebhookService _stripeWebhookService;

    public StripeWebhookController(IStripeWebhookService stripeWebhookService)
    {
        _stripeWebhookService = stripeWebhookService;
    }

    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync(cancellationToken);
        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();

        try
        {
            await _stripeWebhookService.HandleAsync(
                payload,
                signatureHeader,
                cancellationToken);
        }
        catch (StripeException)
        {
            return BadRequest();
        }

        return Ok();
    }
}
