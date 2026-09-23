using Biogarden.BusinessLogic;
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
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private const long MaximumImageSize = 5 * 1024 * 1024;
    private readonly IPostService _postService;
    private readonly IWebHostEnvironment _environment;

    public AdminPostsController(IPostService postService, IWebHostEnvironment environment)
    {
        _postService = postService;
        _environment = environment;
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

        if (post.Image is not null &&
            !string.Equals(previousPost.Image, post.Image, StringComparison.OrdinalIgnoreCase) &&
            !await _postService.IsImageInUseAsync(previousPost.Image, cancellationToken))
        {
            DeleteManagedImage(previousPost.Image);
        }

        return NoContent();
    }

    [HttpDelete("{postId:int}")]
    public async Task<IActionResult> Delete(int postId, CancellationToken cancellationToken)
    {
        var post = await _postService.GetDetailsAsync(postId, cancellationToken);
        if (post is null || !await _postService.DeleteAsync(postId, cancellationToken)) return NotFound();

        if (!await _postService.IsImageInUseAsync(post.Image, cancellationToken))
        {
            DeleteManagedImage(post.Image);
        }

        return NoContent();
    }

    [HttpPost("image")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [RequestSizeLimit(MaximumImageSize)]
    public async Task<ActionResult<object>> UploadImage(IFormFile image, CancellationToken cancellationToken)
    {
        if (image.Length == 0 || image.Length > MaximumImageSize)
        {
            return BadRequest(new { detail = "A kép mérete legfeljebb 5 MB lehet." });
        }

        var extension = Path.GetExtension(image.FileName);
        if (!AllowedImageExtensions.Contains(extension) || !AllowedImageContentTypes.Contains(image.ContentType))
        {
            return BadRequest(new { detail = "Csak JPG, PNG vagy WEBP kép tölthető fel." });
        }

        var uploadsDirectory = Path.Combine(_environment.WebRootPath, "uploads", "posts");
        Directory.CreateDirectory(uploadsDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsDirectory, fileName);

        await using var stream = System.IO.File.Create(filePath);
        await image.CopyToAsync(stream, cancellationToken);

        var path = $"/uploads/posts/{fileName}";
        return Created(path, new { path });
    }

    [HttpDelete("image")]
    public async Task<IActionResult> DeleteUnusedImage([FromBody] DeletePostImageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Path)) return BadRequest();
        if (await _postService.IsImageInUseAsync(request.Path, cancellationToken))
        {
            return Conflict(new { detail = "A kép jelenleg egy bejegyzéshez tartozik." });
        }

        DeleteManagedImage(request.Path);
        return NoContent();
    }

    private void DeleteManagedImage(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath)) return;

        var normalizedPath = publicPath.Replace('\\', '/');
        if (!normalizedPath.StartsWith("/uploads/posts/", StringComparison.OrdinalIgnoreCase)) return;
        if (string.Equals(normalizedPath, BusinessRules.DefaultPostImagePath, StringComparison.OrdinalIgnoreCase)) return;

        var uploadsDirectory = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "posts"));
        var candidatePath = Path.GetFullPath(Path.Combine(
            _environment.WebRootPath,
            normalizedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

        if (!candidatePath.StartsWith(uploadsDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
        if (System.IO.File.Exists(candidatePath)) System.IO.File.Delete(candidatePath);
    }
}

public sealed class DeletePostImageRequest
{
    public string Path { get; set; } = string.Empty;
}
