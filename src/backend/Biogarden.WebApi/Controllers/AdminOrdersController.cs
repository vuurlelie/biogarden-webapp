using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.DTOs.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/orders")]
public sealed class AdminOrdersController : ControllerBase
{
    private readonly IAdminOrderService _service;

    public AdminOrdersController(IAdminOrderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetAll(CancellationToken token)
    {
        return Ok(await _service.GetAllAsync(token));
    }

    [HttpGet("withdrawal-requests")]
    public async Task<ActionResult<IReadOnlyList<AdminOrderWithdrawalRequestDto>>> GetWithdrawalRequests(CancellationToken token)
    {
        return Ok(await _service.GetWithdrawalRequestsAsync(token));
    }

    [HttpPut("{orderId:guid}")]
    public async Task<ActionResult<OrderDto>> Update(
        Guid orderId,
        AdminOrderUpdateDto changes,
        CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        return Ok(await _service.UpdateAsync(orderId, changes, token, actorId));
    }

    [HttpPut("withdrawal-requests/{requestId:int}/handled")]
    public async Task<ActionResult<AdminOrderWithdrawalRequestDto>> MarkWithdrawalRequestHandled(
        int requestId,
        CancellationToken token)
    {
        return Ok(await _service.MarkWithdrawalRequestHandledAsync(requestId, token));
    }
}
