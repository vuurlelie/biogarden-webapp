using Biogarden.BusinessLogic.DTOs.Common;
using System.Security.Claims;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.WebApi.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[ServiceFilter(typeof(Filters.AccountSecurityMutationFilter))]
[Route("api/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _service;

    public AdminUsersController(IAdminUserService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<AdminUserDto>>> GetAll([FromQuery] AdminUserListQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _service.GetPageAsync(query, cancellationToken));
    }

    [HttpPost("{userId:guid}/admin-role")]
    public async Task<ActionResult<AdminUserDto>> SetAdminRole(
        Guid userId,
        [FromQuery] bool isAdmin,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (!isAdmin && userId == currentUserId)
        {
            return SelfActionBadRequest("A saját fiókod admin jogosultságát nem veheted el.");
        }

        return Ok(await _service.SetAdminRoleAsync(userId, isAdmin, currentUserId, cancellationToken));
    }

    [HttpPost("{userId:guid}/confirm-email")]
    public async Task<ActionResult<AdminUserDto>> ConfirmEmail(Guid userId, CancellationToken cancellationToken)
    {
        return Ok(await _service.ConfirmEmailAsync(userId, cancellationToken));
    }

    [HttpPost("{userId:guid}/deactivate")]
    public async Task<ActionResult<AdminUserDto>> Deactivate(Guid userId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (userId == currentUserId)
        {
            return SelfActionBadRequest("A saját fiókodat nem deaktiválhatod.");
        }

        return Ok(await _service.DeactivateAsync(userId, currentUserId, cancellationToken));
    }

    [HttpPost("{userId:guid}/reactivate")]
    public async Task<ActionResult<AdminUserDto>> Reactivate(Guid userId, CancellationToken cancellationToken)
    {
        return Ok(await _service.ReactivateAsync(userId, cancellationToken));
    }

    [HttpPost("{userId:guid}/erase")]
    public async Task<ActionResult<AdminUserErasureResultDto>> Erase(Guid userId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (userId == currentUserId)
        {
            return SelfActionBadRequest("A saját fiókodat nem törölheted és nem anonimizálhatod.");
        }

        return Ok(await _service.EraseAsync(userId, currentUserId, cancellationToken));
    }

    [HttpPost("requests/{requestId:int}/complete-erasure")]
    public async Task<ActionResult<AdminUserDto>> CompleteErasureRequest(
        int requestId,
        [FromBody] AdminAccountLifecycleDecisionDto decision,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.CompleteErasureRequestAsync(requestId, GetCurrentUserId(), decision, cancellationToken));
    }

    [HttpPost("requests/{requestId:int}/reject")]
    public async Task<ActionResult<AdminUserDto>> RejectRequest(
        int requestId,
        [FromBody] AdminAccountLifecycleDecisionDto decision,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.RejectRequestAsync(requestId, GetCurrentUserId(), decision, cancellationToken));
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedAccessException("Authenticated user id is invalid.");
        }

        return userId;
    }

    private BadRequestObjectResult SelfActionBadRequest(string detail)
    {
        return BadRequest(new ApiErrorResponse
        {
            Title = "Saját fiókon nem végezhető művelet.",
            Status = StatusCodes.Status400BadRequest,
            Detail = detail,
            TraceId = HttpContext.TraceIdentifier
        });
    }
}
