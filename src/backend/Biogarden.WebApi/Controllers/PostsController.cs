using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.BusinessLogic.DTOs.Posts;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Route("api/posts")]
public sealed class PostsController : ControllerBase
{
    private readonly IPostService _postService;

    public PostsController(IPostService postService)
    {
        _postService = postService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<PostListItemDto>), StatusCodes.Status200OK)]
    public async Task<PagedResultDto<PostListItemDto>> GetAll([FromQuery] PostListQueryDto query, CancellationToken cancellationToken)
    {
        return await _postService.GetPagedAsync(query, cancellationToken);
    }

    [HttpGet("{postId:int}")]
    [ProducesResponseType(typeof(PostDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostDetailsDto>> GetDetails(int postId, CancellationToken cancellationToken)
    {
        var post = await _postService.GetDetailsAsync(postId, cancellationToken);

        if (post is null || post.Draft)
        {
            return NotFound();
        }

        return Ok(post);
    }
}
