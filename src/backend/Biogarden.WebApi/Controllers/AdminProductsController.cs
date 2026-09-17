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
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };
    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private const long MaximumImageSize = 5 * 1024 * 1024;
    private readonly IProductService _productService;
    private readonly IWebHostEnvironment _environment;

    public AdminProductsController(IProductService productService, IWebHostEnvironment environment)
    {
        _productService = productService;
        _environment = environment;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<ProductDetailsDto>>> GetAll(
        [FromQuery] ProductListQueryDto query,
        CancellationToken cancellationToken)
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
        if (previousProduct is null) return NotFound();

        await _productService.UpdateAsync(productId, product, cancellationToken);
        if (product.Image is not null &&
            !string.Equals(previousProduct.Image, product.Image, StringComparison.OrdinalIgnoreCase) &&
            !await _productService.IsImageInUseAsync(previousProduct.Image, cancellationToken))
            DeleteManagedImage(previousProduct.Image);

        return NoContent();
    }

    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> Delete(int productId, CancellationToken cancellationToken)
    {
        var product = await _productService.GetDetailsAsync(productId, cancellationToken);
        if (product is null || !await _productService.DeleteAsync(productId, cancellationToken)) return NotFound();
        if (!await _productService.IsImageInUseAsync(product.Image, cancellationToken))
            DeleteManagedImage(product.Image);
        return NoContent();
    }

    [HttpPost("image")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [RequestSizeLimit(MaximumImageSize)]
    public async Task<ActionResult<object>> UploadImage(IFormFile image, CancellationToken cancellationToken)
    {
        if (image.Length == 0 || image.Length > MaximumImageSize)
            return BadRequest(new { detail = "A kép mérete legfeljebb 5 MB lehet." });

        var extension = Path.GetExtension(image.FileName);
        if (!AllowedImageExtensions.Contains(extension) || !AllowedImageContentTypes.Contains(image.ContentType))
            return BadRequest(new { detail = "Csak JPG, PNG vagy WEBP kép tölthető fel." });

        var uploadsDirectory = Path.Combine(_environment.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(uploadsDirectory);
        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsDirectory, fileName);

        await using var stream = System.IO.File.Create(filePath);
        await image.CopyToAsync(stream, cancellationToken);

        var path = $"/uploads/products/{fileName}";
        return Created(path, new { path });
    }

    [HttpDelete("image")]
    public async Task<IActionResult> DeleteUnusedImage([FromBody] DeleteProductImageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Path)) return BadRequest();
        if (await _productService.IsImageInUseAsync(request.Path, cancellationToken))
            return Conflict(new { detail = "A kép jelenleg egy termékhez tartozik." });

        DeleteManagedImage(request.Path);
        return NoContent();
    }

    private void DeleteManagedImage(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath)) return;
        var normalizedPath = publicPath.Replace('\\', '/');
        if (!normalizedPath.StartsWith("/uploads/products/", StringComparison.OrdinalIgnoreCase)) return;
        if (string.Equals(Path.GetFileName(normalizedPath), "defaultimg.png", StringComparison.OrdinalIgnoreCase)) return;

        var uploadsDirectory = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "products"));
        var candidatePath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, normalizedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!candidatePath.StartsWith(uploadsDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
        if (System.IO.File.Exists(candidatePath)) System.IO.File.Delete(candidatePath);
    }
}

public sealed class DeleteProductImageRequest
{
    public string Path { get; set; } = string.Empty;
}
