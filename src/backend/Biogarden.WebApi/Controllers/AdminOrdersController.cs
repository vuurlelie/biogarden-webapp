using Biogarden.BusinessLogic.DTOs.Common;
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
    public async Task<ActionResult<PagedResultDto<OrderDto>>> GetAll([FromQuery] AdminOrderListQuery query, CancellationToken token)
    {
        return Ok(await _service.GetPageAsync(query, token));
    }

    [HttpGet("withdrawal-requests")]
    public async Task<ActionResult<PagedResultDto<AdminOrderRequestThreadDto>>> GetWithdrawalRequests([FromQuery] AdminWithdrawalListQuery query, CancellationToken token)
    {
        return Ok(await _service.GetWithdrawalRequestsAsync(query, token));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken token)
    {
        var result = await _service.GetByIdAsync(id, token);
        return result is null ? NotFound() : Ok(result);
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
        CancellationToken token,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] HandleOrderRequestDto? body = null)
    {
        return Ok(await _service.MarkWithdrawalRequestHandledAsync(requestId, token, body?.AdminResponse));
    }
}
