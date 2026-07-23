using Biogarden.BusinessLogic.DTOs.Products;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Enums;
using Biogarden.DataAccess.Models;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class ProductServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);
    private readonly Mock<IProductRepository> _products = new(MockBehavior.Strict);
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _unitOfWork.SetupGet(x => x.ProductRepository).Returns(_products.Object);
        _service = new ProductService(_unitOfWork.Object);
    }

    [Fact]
    public async Task GetPagedAsync_ClampsPageSizeAndMapsCategoryGroup()
    {
        var query = new ProductListQueryDto
        {
            Page = -4,
            PageSize = 500,
            Category = "fuszerek-gyogyteak",
            Sort = "price-desc",
            AvailableOnly = true,
            DeliveryOnly = true
        };

        _products
            .Setup(repository => repository.GetPagedAsync(
                It.Is<IReadOnlyCollection<ProductCategory>>(categories =>
                    categories.SequenceEqual(new[] { ProductCategory.HerbalTea, ProductCategory.Spice })),
                true,
                true,
                "price-desc",
                1,
                60,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Product>(
                [new Product { Id = 5, Name = "Tea", Price = 1200 }],
                1,
                60,
                1));

        var result = await _service.GetPagedAsync(query);

        Assert.Equal(1, result.Page);
        Assert.Equal(60, result.PageSize);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Tea", Assert.Single(result.Items).Name);
        _products.VerifyAll();
        _unitOfWork.VerifyNoOtherCalls();
    }
}
