using Biogarden.WebApi.Services;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.BusinessLogic.DTOs.Posts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/posts")]
public sealed class AdminPostsController : ControllerBase
{
    private readonly IPostService _postService;
    private readonly ManagedImageStorage _images;

    public AdminPostsController(IPostService postService, ManagedImageStorage images)
    {
        _postService = postService;
        _images = images;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<PostListItemDto>>> GetAll(
        [FromQuery] PostListQueryDto query,
        CancellationToken cancellationToken)
        => Ok(await _postService.GetAdminPagedAsync(query, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    public async Task<ActionResult<int>> Create(PostCreateDto post, CancellationToken cancellationToken)
    {
        var postId = await _postService.CreateAsync(post, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, postId);
    }

    [HttpPut("{postId:int}")]
    public async Task<IActionResult> Update(int postId, PostEditDto post, CancellationToken cancellationToken)
    {
        var previousPost = await _postService.GetDetailsAsync(postId, cancellationToken);
        if (previousPost is null) return NotFound();

        await _postService.UpdateAsync(postId, post, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{postId:int}")]
    public async Task<IActionResult> Delete(int postId, CancellationToken cancellationToken)
    {
        var post = await _postService.GetDetailsAsync(postId, cancellationToken);
        if (post is null || !await _postService.DeleteAsync(postId, cancellationToken)) return NotFound();

        return NoContent();
    }

    [HttpPost("image")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [RequestSizeLimit(ManagedImageStorage.MaximumRequestBytes)]
    public async Task<ActionResult<object>> UploadImage(IFormFile image, CancellationToken cancellationToken)
    {
        var path = await _images.SaveAsync(image, ImageCategory.Posts, cancellationToken);
        return Created(path, new { path });
    }

    // Older clients may request cleanup after a failed save. Never race a concurrent
    // assignment (or an ambiguous successful save) by physically deleting its image.
    [HttpDelete("image")]
    public IActionResult DeleteUnusedImage([FromBody] DeletePostImageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path)) return BadRequest();
        return Conflict(new { detail = ManagedImageStorage.RetentionMessage });
    }
}

public sealed class DeletePostImageRequest
{
    public string Path { get; set; } = string.Empty;
}
