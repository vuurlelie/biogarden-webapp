using Biogarden.BusinessLogic.DTOs.Posts;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Biogarden.DataAccess.Models;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class PostServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);
    private readonly Mock<IPostRepository> _posts = new(MockBehavior.Strict);
    private readonly PostService _service;

    public PostServiceTests()
    {
        _unitOfWork.SetupGet(unitOfWork => unitOfWork.PostRepository).Returns(_posts.Object);
        _service = new PostService(_unitOfWork.Object);
    }

    [Fact]
    public async Task GetPagedAsync_RequestsPublicPostsOnly()
    {
        var query = new PostListQueryDto
        {
            Page = 2,
            PageSize = 4,
            Category = PostCategory.Recipe,
            Sort = "oldest"
        };

        _posts
            .Setup(repository => repository.GetPagedAsync(null, PostCategory.Recipe, true, null, "oldest", 2, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Post>(
                [new Post { Id = 3, Title = "Recept", Article = "Legalabb husz karakteres szoveg" }],
                2,
                4,
                5));

        var result = await _service.GetPagedAsync(query);

        Assert.Equal(2, result.Page);
        Assert.Equal(4, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal("Recept", Assert.Single(result.Items).Title);
        _posts.VerifyAll();
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAdminPagedAsync_AllowsDraftFiltering()
    {
        var query = new PostListQueryDto
        {
            Search = "palanta",
            Draft = true
        };

        _posts
            .Setup(repository => repository.GetPagedAsync("palanta", null, false, true, string.Empty, 1, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Post>(
                [new Post { Id = 4, Title = "Piszkozat", Article = "Legalabb husz karakteres szoveg", Draft = true }],
                1,
                12,
                1));

        var result = await _service.GetAdminPagedAsync(query);

        Assert.True(Assert.Single(result.Items).Draft);
        _posts.VerifyAll();
        _unitOfWork.VerifyNoOtherCalls();
    }
}
