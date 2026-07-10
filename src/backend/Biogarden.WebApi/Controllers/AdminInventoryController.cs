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
    public async Task<ActionResult<IReadOnlyList<AdminInventoryItemDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _inventoryService.GetAllAsync(cancellationToken));

    [HttpPut("{productId:int}")]
    public async Task<ActionResult<AdminInventoryItemDto>> Update(int productId, UpdateAdminInventoryDto changes, CancellationToken cancellationToken)
        => Ok(await _inventoryService.UpdateAsync(productId, changes, cancellationToken));
}
