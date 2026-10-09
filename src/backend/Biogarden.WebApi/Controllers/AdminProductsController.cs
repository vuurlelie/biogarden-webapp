using Biogarden.WebApi.Services;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.BusinessLogic.DTOs.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/products")]
public sealed class AdminProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ManagedImageStorage _images;

    public AdminProductsController(IProductService productService, ManagedImageStorage images)
    {
        _productService = productService;
        _images = images;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<ProductDetailsDto>>> GetAll([FromQuery] ProductListQueryDto query, CancellationToken cancellationToken)
        => Ok(await _productService.GetPagedDetailsAsync(query, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    public async Task<ActionResult<int>> Create(ProductCreateDto product, CancellationToken cancellationToken)
    {
        var productId = await _productService.CreateAsync(product, cancellationToken);
        return CreatedAtAction(nameof(ProductsController.GetDetails), "Products", new { productId }, productId);
    }

    [HttpPut("{productId:int}")]
    public async Task<IActionResult> Update(int productId, ProductEditDto product, CancellationToken cancellationToken)
    {
        var previousProduct = await _productService.GetDetailsAsync(productId, cancellationToken);
        if (previousProduct is null)
            return NotFound();

        await _productService.UpdateAsync(productId, product, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> Delete(int productId, CancellationToken cancellationToken)
    {
        var product = await _productService.GetDetailsAsync(productId, cancellationToken);
        if (product is null || !await _productService.DeleteAsync(productId, cancellationToken))
            return NotFound();
        return NoContent();
    }

    [HttpPost("image")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [RequestSizeLimit(ManagedImageStorage.MaximumRequestBytes)]
    public async Task<ActionResult<object>> UploadImage(IFormFile image, CancellationToken cancellationToken)
    {
        var path = await _images.SaveAsync(image, ImageCategory.Products, cancellationToken);
        return Created(path, new { path });
    }

    // Older clients may request cleanup after a failed save. Never race a concurrent
    // assignment (or an ambiguous successful save) by physically deleting its image.
    [HttpDelete("image")]
    public IActionResult DeleteUnusedImage([FromBody] DeleteProductImageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
            return BadRequest();
        return Conflict(new { detail = ManagedImageStorage.RetentionMessage });
    }
}

public sealed class DeleteProductImageRequest
{
    public string Path { get; set; } = string.Empty;
}
