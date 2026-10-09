using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Biogarden.BusinessLogic.Constants;
using Biogarden.WebApi.Authentication;
using Microsoft.AspNetCore.Authentication;
using Biogarden.WebApi.Filters;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.WebApi.Constants;
using Biogarden.WebApi.Contracts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [EnableRateLimiting(RateLimitPolicyNames.SensitiveAuth)]
    public async Task<IActionResult> RegisterAsync(
        [FromBody] RegisterRequestDto registerRequestDto)
    {
        var result = await _authService.RegisterAsync(registerRequestDto);

        if (!result.Succeeded)
        {
            return BadRequest(new ApiErrorResponse
            {
                Title = "Registration failed.",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.ErrorMessage,
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return StatusCode(StatusCodes.Status201Created);
    }

    [ProducesResponseType(typeof(BrowserSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [HttpPost("login")]
    [BrowserSessionRequest]
    [EnableRateLimiting(RateLimitPolicyNames.SensitiveAuth)]
    public async Task<IActionResult> LoginAsync(
        [FromBody] LoginRequestDto loginRequestDto)
    {
        var result = await _authService.LoginAsync(loginRequestDto);

        if (!result.Succeeded)
        {
            return Unauthorized(new ApiErrorResponse
            {
                Title = "Authentication failed.",
                Status = StatusCodes.Status401Unauthorized,
                Detail = result.ErrorMessage,
                TraceId = HttpContext.TraceIdentifier
            });
        }

        // The internal service token is never returned to browser clients.
        var auth = result.AuthResponse!;
        var issued = new JwtSecurityTokenHandler().ReadJwtToken(auth.Token);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.UserId.ToString()),
            new(ClaimTypes.Email, auth.Email),
            new(AuthClaimTypes.SecurityStamp, issued.Claims.Single(claim => claim.Type == AuthClaimTypes.SecurityStamp).Value),
            new(BrowserAuthentication.SessionClaim, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(auth.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, BrowserAuthentication.Scheme));
        var clock = HttpContext.RequestServices.GetRequiredService<TimeProvider>();
        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            IssuedUtc = clock.GetUtcNow(),
            ExpiresUtc = clock.GetUtcNow().Add(BrowserAuthentication.IdleLifetime)
        };
        await HttpContext.SignOutAsync(BrowserAuthentication.Scheme);
        // This runtime caches the removed ticket-store key inside its original handler.
        // A fresh framework handler issues a new key rather than renewing the revoked one.
        var handler = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationHandler>();
        await handler.InitializeAsync(
            new AuthenticationScheme(BrowserAuthentication.Scheme, null, typeof(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationHandler)),
            HttpContext
        );
        await handler.SignInAsync(principal, properties);
        return Ok(BrowserAuthentication.Describe(principal, properties));
    }

    [ProducesResponseType(typeof(BrowserSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpGet("session")]
    [BrowserSessionRequest(ProtectReads = true)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Session()
    {
        var session = await HttpContext.AuthenticateAsync(BrowserAuthentication.Scheme);
        return session.Succeeded ? Ok(BrowserAuthentication.Describe(session.Principal!, session.Properties!)) : NoContent();
    }

    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpPost("logout")]
    [BrowserSessionRequest]
    public async Task<IActionResult> Logout()
    {
        var session = await HttpContext.AuthenticateAsync(BrowserAuthentication.Scheme);
        if (session.Succeeded)
        {
            var id = Guid.Parse(session.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var guestId = Guid.NewGuid().ToString();
            await HttpContext.RequestServices.GetRequiredService<Biogarden.BusinessLogic.Services.CheckoutDraftService>()
                .TransferCartToGuestAsync(id, guestId, HttpContext.RequestAborted);
            Response.Cookies.Append("biogarden_cart_id", guestId, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddMinutes(30),
                IsEssential = true
            });
        }
        await HttpContext.SignOutAsync(BrowserAuthentication.Scheme);
        return NoContent();
    }

    [HttpPost("confirm-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EnableRateLimiting(RateLimitPolicyNames.SensitiveAuth)]
    public async Task<IActionResult> ConfirmEmailAsync(
        [FromBody] ConfirmEmailRequestDto confirmEmailRequestDto)
    {
        var result = await _authService.ConfirmEmailAsync(confirmEmailRequestDto);

        if (!result.Succeeded)
        {
            return BadRequest(new ApiErrorResponse
            {
                Title = "Email confirmation failed.",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.ErrorMessage,
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return NoContent();
    }

    [HttpPost("resend-email-confirmation")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [EnableRateLimiting(RateLimitPolicyNames.SensitiveAuth)]
    public async Task<IActionResult> ResendEmailConfirmationAsync(
        [FromBody] ResendEmailConfirmationRequestDto resendEmailConfirmationRequestDto)
    {
        await _authService.ResendEmailConfirmationAsync(resendEmailConfirmationRequestDto);

        return Accepted();
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [EnableRateLimiting(RateLimitPolicyNames.SensitiveAuth)]
    public async Task<IActionResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequestDto forgotPasswordRequestDto)
    {
        await _authService.ForgotPasswordAsync(forgotPasswordRequestDto);

        return Accepted();
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EnableRateLimiting(RateLimitPolicyNames.SensitiveAuth)]
    public async Task<IActionResult> ResetPasswordAsync(
        [FromBody] ResetPasswordRequestDto resetPasswordRequestDto)
    {
        var result = await _authService.ResetPasswordAsync(resetPasswordRequestDto);

        if (!result.Succeeded)
        {
            return BadRequest(new ApiErrorResponse
            {
                Title = "Password reset failed.",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.ErrorMessage,
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return NoContent();
    }
}
