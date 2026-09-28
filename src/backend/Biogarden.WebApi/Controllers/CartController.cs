using System.Security.Claims;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.WebApi.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private const string CartCookieName = "biogarden_cart_id";
    private const int CartCookieExpirationInMinutes = 30;

    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> GetCart(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var sessionId = GetOrCreateSessionId();

        var cart = await _cartService.GetCartAsync(
            userId,
            sessionId,
            cancellationToken);

        EnsureCartCookie(sessionId);

        return Ok(cart);
    }

    [HttpPost("items")]
    [EnableRateLimiting(RateLimitPolicyNames.CartMutation)]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CartDto>> AddItem(
        [FromBody] AddCartItemDto addCartItemDto,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var sessionId = GetOrCreateSessionId();

            var cart = await _cartService.AddItemAsync(
                userId,
                sessionId,
                addCartItemDto,
                cancellationToken);

            EnsureCartCookie(sessionId);

            return Ok(cart);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpGet("products/{productId:int}/availability")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetProductAvailability(
        int productId,
        CancellationToken cancellationToken)
    {
        try
        {
            var availableQuantity = await _cartService.GetAvailableQuantityToAddAsync(
                GetCurrentUserId(), GetSessionId(), productId, cancellationToken);
            return Ok(new { availableQuantity });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpPut("items/{cartItemId:guid}")]
    [EnableRateLimiting(RateLimitPolicyNames.CartMutation)]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartDto>> UpdateItemQuantity(
        Guid cartItemId,
        [FromBody] UpdateCartItemQuantityDto updateCartItemQuantityDto,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var sessionId = GetOrCreateSessionId();

            var cart = await _cartService.UpdateItemQuantityAsync(
                userId,
                sessionId,
                cartItemId,
                updateCartItemQuantityDto,
                cancellationToken);

            EnsureCartCookie(sessionId);

            return Ok(cart);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpDelete("items/{cartItemId:guid}")]
    [EnableRateLimiting(RateLimitPolicyNames.CartMutation)]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartDto>> RemoveItem(
        Guid cartItemId,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var sessionId = GetOrCreateSessionId();

            var cart = await _cartService.RemoveItemAsync(
                userId,
                sessionId,
                cartItemId,
                cancellationToken);

            EnsureCartCookie(sessionId);

            return Ok(cart);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpDelete]
    [EnableRateLimiting(RateLimitPolicyNames.CartMutation)]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartDto>> ClearCart(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var sessionId = GetOrCreateSessionId();

            var cart = await _cartService.ClearCartAsync(
                userId,
                sessionId,
                cancellationToken);

            EnsureCartCookie(sessionId);

            return Ok(cart);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpGet("validate")]
    [ProducesResponseType(typeof(CartValidationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartValidationResultDto>> ValidateCart(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var sessionId = GetOrCreateSessionId();

        var validationResult = await _cartService.ValidateCartForCheckoutAsync(
            userId,
            sessionId,
            cancellationToken);

        EnsureCartCookie(sessionId);

        return Ok(validationResult);
    }

    [Authorize]
    [HttpPost("merge")]
    [EnableRateLimiting(RateLimitPolicyNames.CartMutation)]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CartDto>> MergeGuestCartIntoUserCart(
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();

            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "A felhasználó nincs bejelentkezve." });
            }

            var guestSessionId = GetSessionId();

            if (string.IsNullOrWhiteSpace(guestSessionId))
            {
                var userCart = await _cartService.GetCartAsync(
                    userId,
                    sessionId: null,
                    cancellationToken);

                return Ok(userCart);
            }

            var mergedCart = await _cartService.MergeGuestCartIntoUserCartAsync(
                userId.Value,
                guestSessionId,
                cancellationToken);

            EnsureCartCookie(guestSessionId);

            return Ok(mergedCart);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CartDto>> GetMyCart(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var cart = await _cartService.GetCartAsync(
            userId,
            sessionId: null,
            cancellationToken);

        return Ok(cart);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue))
        {
            return null;
        }

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }

    private string? GetSessionId()
    {
        if (Request.Cookies.TryGetValue(CartCookieName, out var sessionId) && Guid.TryParse(sessionId, out var parsed))
        {
            return parsed.ToString();
        }

        return null;
    }

    private string GetOrCreateSessionId()
    {
        var existingSessionId = GetSessionId();

        if (!string.IsNullOrWhiteSpace(existingSessionId))
        {
            return existingSessionId;
        }

        return Guid.NewGuid().ToString();
    }

    private void EnsureCartCookie(string sessionId)
    {
        Response.Cookies.Append(
            CartCookieName,
            sessionId,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                // Hosted frontend and API can be on different sites. Cross-site cookies
                // require HTTPS; retain Lax for plain-HTTP local development.
                SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddMinutes(CartCookieExpirationInMinutes),
                IsEssential = true
            });
    }
}
