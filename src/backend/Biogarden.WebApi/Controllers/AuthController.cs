using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.WebApi.Contracts;
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

        return Ok();
    }

    [HttpPost("login")]
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

        return Ok();
    }

    [HttpPost("resend-email-confirmation")]
    public async Task<IActionResult> ResendEmailConfirmationAsync(
        [FromBody] ResendEmailConfirmationRequestDto resendEmailConfirmationRequestDto)
    {
        await _authService.ResendEmailConfirmationAsync(resendEmailConfirmationRequestDto);

        return Ok();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequestDto forgotPasswordRequestDto)
    {
        await _authService.ForgotPasswordAsync(forgotPasswordRequestDto);

        return Ok();
    }

    [HttpPost("reset-password")]
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

        return Ok();
    }
}
