using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Addresses;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Route("api/addresses")]
[Authorize(Roles = "Admin")]
public class AddressesController : ControllerBase
{
    private readonly IAddressService _addressService;

    public AddressesController(IAddressService addressService)
    {
        _addressService = addressService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AddressListItemDto>>> GetAllAsync()
    {
        var addresses = await _addressService.GetAllAsync();

        return Ok(addresses);
    }

    [HttpGet("{addressId:int}")]
    public async Task<ActionResult<AddressDetailsDto>> GetByIdAsync(int addressId)
    {
        var address = await _addressService.GetDetailsAsync(addressId);

        if (address is null)
        {
            return NotFound();
        }

        return Ok(address);
    }

    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] AddressCreateDto addressCreateDto)
    {
        var addressId = await _addressService.CreateAsync(addressCreateDto);

        return CreatedAtAction(nameof(GetByIdAsync), new { addressId }, addressId);
    }

    [HttpPut("{addressId:int}")]
    public async Task<IActionResult> UpdateAsync(int addressId, [FromBody] AddressEditDto addressEditDto)
    {
        await _addressService.UpdateAsync(addressId, addressEditDto);

        return NoContent();
    }

    [HttpDelete("{addressId:int}")]
    public async Task<IActionResult> DeleteAsync(int addressId)
    {
        await _addressService.DeleteAsync(addressId);

        return NoContent();
    }
}
