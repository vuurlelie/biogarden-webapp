using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Common;
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
    [ProducesResponseType(typeof(PagedResultDto<ProductListItemDto>), StatusCodes.Status200OK)]
    public async Task<PagedResultDto<ProductListItemDto>> GetAll([FromQuery] ProductListQueryDto query, CancellationToken cancellationToken)
    {
        return await _productService.GetPagedAsync(query, cancellationToken);
    }

    [HttpGet("{productId:int}")]
    [ProducesResponseType(typeof(ProductDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailsDto>> GetDetails(int productId, CancellationToken cancellationToken)
    {
        var product = await _productService.GetDetailsAsync(productId, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }
}
