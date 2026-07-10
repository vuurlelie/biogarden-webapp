using System.Security.Claims;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IUserService _userService;

    public ProfileController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<UserProfileDto>> GetProfileAsync()
    {
        var userId = GetCurrentUserId();

        var profile = await _userService.GetProfileAsync(userId);

        if (profile is null)
        {
            return NotFound();
        }

        return Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfileAsync(
        [FromBody] UpdateUserProfileDto updateUserProfileDto)
    {
        var userId = GetCurrentUserId();

        await _userService.UpdateProfileAsync(userId, updateUserProfileDto);

        return NoContent();
    }

    [HttpGet("addresses")]
    public async Task<ActionResult<UserAddressesDto>> GetAddressesAsync()
    {
        var userId = GetCurrentUserId();

        var addresses = await _userService.GetAddressesAsync(userId);

        if (addresses is null)
        {
            return NotFound();
        }

        return Ok(addresses);
    }

    [HttpPut("addresses")]
    public async Task<IActionResult> UpdateAddressesAsync(
        [FromBody] UpdateUserAddressesDto updateUserAddressesDto)
    {
        var userId = GetCurrentUserId();

        await _userService.UpdateAddressesAsync(
            userId,
            updateUserAddressesDto);

        return NoContent();
    }

    [HttpPost("account-lifecycle")]
    public async Task<ActionResult<AccountLifecycleResultDto>> RequestAccountLifecycleAsync(
        [FromBody] AccountLifecycleRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        return Ok(await _userService.RequestAccountLifecycleAsync(
            userId,
            request,
            cancellationToken));
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user id is invalid.");
        }

        return userId;
    }
}
