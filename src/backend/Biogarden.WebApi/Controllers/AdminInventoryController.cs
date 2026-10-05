using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/inventory")]
public sealed class AdminInventoryController : ControllerBase
{
    private readonly IAdminInventoryService _inventoryService;
    public AdminInventoryController(IAdminInventoryService inventoryService) => _inventoryService = inventoryService;

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<AdminInventoryItemDto>>> GetAll([FromQuery] AdminInventoryListQuery query, CancellationToken cancellationToken)
        => Ok(await _inventoryService.GetPageAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminInventoryItemDto>> GetById(int id, CancellationToken token)
    {
        var result = await _inventoryService.GetByIdAsync(id, token);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{productId:int}")]
    public async Task<ActionResult<AdminInventoryItemDto>> Update(int productId, UpdateAdminInventoryDto changes, CancellationToken cancellationToken)
        => Ok(await _inventoryService.UpdateAsync(productId, changes, cancellationToken));
}
