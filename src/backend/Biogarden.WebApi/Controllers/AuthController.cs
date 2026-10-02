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

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicyNames.SensitiveAuth)]
    public async Task<ActionResult<AuthResponseDto>> LoginAsync(
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

        return Ok(result.AuthResponse);
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
