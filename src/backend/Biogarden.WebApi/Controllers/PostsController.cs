using Biogarden.BusinessLogic.Abstractions;
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
    [ProducesResponseType(typeof(IReadOnlyList<PostListItemDto>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PostListItemDto>> GetAll(CancellationToken cancellationToken)
    {
        return await _postService.GetAllAsync(cancellationToken);
    }

    [HttpGet("{postId:int}")]
    [ProducesResponseType(typeof(PostDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostDetailsDto>> GetDetails(int postId, CancellationToken cancellationToken)
    {
        var post = await _postService.GetDetailsAsync(postId, cancellationToken);

        if (post is null)
        {
            return NotFound();
        }

        return Ok(post);
    }

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    public async Task<ActionResult<int>> Create(PostCreateDto newPost, CancellationToken cancellationToken)
    {
        var postId = await _postService.CreateAsync(newPost, cancellationToken);

        return CreatedAtAction(
            nameof(GetDetails),
            new { postId },
            postId);
    }

    [HttpPut("{postId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int postId, PostEditDto changes, CancellationToken cancellationToken)
    {
        await _postService.UpdateAsync(postId, changes, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{postId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int postId, CancellationToken cancellationToken)
    {
        var deleted = await _postService.DeleteAsync(postId, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}