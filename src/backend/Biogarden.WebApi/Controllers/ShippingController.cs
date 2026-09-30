using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.DataAccess.Enums;
using Biogarden.WebApi.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Route("api/shipping/gls/points")]
public sealed class ShippingController(IGlsDeliveryPointDirectory directory) : ControllerBase
{
    [HttpGet("{id}")]
    [EnableRateLimiting(RateLimitPolicyNames.CartMutation)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<GlsDeliveryPointDto>> Get(string id, [FromQuery] FulfillmentMethod type, CancellationToken cancellationToken)
        => Ok(await directory.ResolveAsync(id, type, cancellationToken));
}
