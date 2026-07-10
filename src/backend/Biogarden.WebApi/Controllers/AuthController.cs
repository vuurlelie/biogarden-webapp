using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
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
        await _authService.RegisterAsync(registerRequestDto);

        return Ok();
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> LoginAsync(
        [FromBody] LoginRequestDto loginRequestDto)
    {
        var result = await _authService.LoginAsync(loginRequestDto);

        return Ok(result);
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmailAsync(
        [FromBody] ConfirmEmailRequestDto confirmEmailRequestDto)
    {
        await _authService.ConfirmEmailAsync(confirmEmailRequestDto);

        return Ok();
    }

    [HttpPost("resend-email-confirmation")]
    public async Task<IActionResult> ResendEmailConfirmationAsync(
        [FromBody] ResendEmailConfirmationRequestDto resendEmailConfirmationRequestDto)
    {
        await _authService.ResendEmailConfirmationAsync(resendEmailConfirmationRequestDto);

        return Ok();
    }
}
