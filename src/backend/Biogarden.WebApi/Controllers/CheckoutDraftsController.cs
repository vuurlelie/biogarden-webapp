using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Biogarden.BusinessLogic.DTOs.Checkout;
using Biogarden.BusinessLogic.Services;
using Biogarden.WebApi.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Biogarden.WebApi.Filters.BrowserSessionRequest(ProtectReads = true)]
[Route("api/checkout-drafts")]
[EnableRateLimiting(RateLimitPolicyNames.CartMutation)]
[RequestSizeLimit(16384)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CheckoutDraftsController(CheckoutDraftService drafts) : Controller
{
    private Guid? UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private string? GuestId => Guid.TryParse(Request.Cookies["biogarden_cart_id"], out var id) ? id.ToString() : null;
    private string Session => UserId.HasValue
        ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Request.Headers.Authorization.ToString())))
        : "guest";

    [HttpGet("{cartId:guid}")]
    [ProducesResponseType(typeof(CheckoutDraftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<CheckoutDraftDto> Read(Guid cartId, CancellationToken ct)
        => drafts.ReadAsync(cartId, UserId, GuestId, Session, ct);

    [HttpPut("{cartId:guid}")]
    [ProducesResponseType(typeof(CheckoutDraftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<CheckoutDraftDto> Save(Guid cartId, SaveCheckoutDraftDto request, CancellationToken ct)
        => drafts.SaveAsync(cartId, UserId, GuestId, Session, request, ct);

    [HttpDelete("{cartId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid cartId, [FromQuery] Guid? version, CancellationToken ct)
    {
        await drafts.DeleteAsync(cartId, UserId, GuestId, Session, version, ct);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("session")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EndSession(CancellationToken ct)
    {
        await drafts.ClearSessionAsync(UserId!.Value, Session, ct);
        return NoContent();
    }
}
