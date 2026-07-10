using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.DTOs.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController, Authorize(Roles = "Admin"), Route("api/admin/orders")]
public sealed class AdminOrdersController : ControllerBase
{
    private readonly IAdminOrderService _service;
    public AdminOrdersController(IAdminOrderService service) => _service = service;
    [HttpGet] public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetAll(CancellationToken token) => Ok(await _service.GetAllAsync(token));
    [HttpPut("{orderId:guid}")] public async Task<ActionResult<OrderDto>> Update(Guid orderId, AdminOrderUpdateDto changes, CancellationToken token) => Ok(await _service.UpdateAsync(orderId, changes, token));
}
