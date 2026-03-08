using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Products;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductListItemDto>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ProductListItemDto>> GetAll(CancellationToken cancellationToken)
    {
        return await _productService.GetAllAsync(cancellationToken);
    }

    [HttpGet("{productId:int}")]
    [ProducesResponseType(typeof(ProductDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailsDto>> GetDetails(int productId, CancellationToken cancellationToken)
    {
        var product = await _productService.GetDetailsAsync(productId, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    public async Task<ActionResult<int>> Create(ProductCreateDto newProduct, CancellationToken cancellationToken)
    {
        var productId = await _productService.CreateAsync(newProduct, cancellationToken);

        return CreatedAtAction(
            nameof(GetDetails),
            new { productId },
            productId);
    }

    [HttpPut("{productId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int productId, ProductEditDto changes, CancellationToken cancellationToken)
    {
        await _productService.UpdateAsync(productId, changes, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{productId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int productId, CancellationToken cancellationToken)
    {
        var deleted = await _productService.DeleteAsync(productId, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}