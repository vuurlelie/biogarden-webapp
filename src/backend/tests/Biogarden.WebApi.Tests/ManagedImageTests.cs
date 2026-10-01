using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Biogarden.WebApi.Services;
using ImageMagick;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Biogarden.WebApi.Tests;

public sealed class ManagedImageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"BiogardenImageTests_{Guid.NewGuid():N}");

    [Theory]
    [InlineData("posts", "jpg", "image/jpeg", MagickFormat.Jpeg)]
    [InlineData("posts", "png", "image/png", MagickFormat.Png)]
    [InlineData("posts", "webp", "image/webp", MagickFormat.WebP)]
    [InlineData("products", "jpg", "image/jpeg", MagickFormat.Jpeg)]
    [InlineData("products", "png", "image/png", MagickFormat.Png)]
    [InlineData("products", "webp", "image/webp", MagickFormat.WebP)]
    public async Task Upload_DecodesReencodesAndPublishesBothCategories(string category, string extension, string mime, MagickFormat format)
    {
        Directory.CreateDirectory(_root);
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Test-Role", "Admin");
        var data = ImageBytes(format).Concat(Encoding.UTF8.GetBytes("<script>trailing-payload</script>")).ToArray();
        using var content = Multipart(data, $"../../untrusted.{extension}", mime);
        using var response = await client.PostAsync($"/api/admin/{category}/image", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ImageResponse>();
        Assert.Matches($"^/uploads/{category}/[a-f0-9]{{32}}\\.{extension}$", result!.Path);
        Assert.Equal(result.Path, response.Headers.Location!.OriginalString);
        var saved = await File.ReadAllBytesAsync(Path.Combine(_root, result.Path.TrimStart('/')));
        Assert.DoesNotContain("trailing-payload", Encoding.UTF8.GetString(saved));
        using var decoded = new MagickImage(saved);
        Assert.Equal(16u, decoded.Width);
        Assert.Equal(12u, decoded.Height);
        Assert.Empty(decoded.ProfileNames);
        using var published = await client.GetAsync(result.Path);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal(mime, published.Content.Headers.ContentType!.MediaType);
        Assert.Empty(Directory.GetFiles(_root, "*.uploading", SearchOption.AllDirectories));

        // Even a legacy cleanup request must not delete a file another save may reference.
        using var deletion = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/{category}/image")
        { Content = JsonContent.Create(new { path = result.Path }) };
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(deletion)).StatusCode);
        Assert.True(File.Exists(Path.Combine(_root, result.Path.TrimStart('/'))));
    }

    [Theory]
    [InlineData("posts", null, HttpStatusCode.Unauthorized)]
    [InlineData("products", null, HttpStatusCode.Unauthorized)]
    [InlineData("posts", "Customer", HttpStatusCode.Forbidden)]
    [InlineData("products", "Customer", HttpStatusCode.Forbidden)]
    public async Task Upload_RequiresAdmin(string category, string? role, HttpStatusCode status)
    {
        Directory.CreateDirectory(_root);
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        if (role is not null) client.DefaultRequestHeaders.Add("Test-Role", role);
        using var content = Multipart(ImageBytes(MagickFormat.Png), "photo.png", "image/png");
        Assert.Equal(status, (await client.PostAsync($"/api/admin/{category}/image", content)).StatusCode);
        Assert.Empty(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("fake.jpg", "image/jpeg", "<svg onload='alert(1)'/>")]
    [InlineData("fake.png", "image/png", "This is not an image")]
    [InlineData("fake.svg", "image/svg+xml", "<svg/>")]
    [InlineData("fake.webp", "image/webp", "RIFF1234WEBPinvalid")]
    public async Task Upload_RejectsRenamedAndCorruptFiles(string name, string mime, string contents)
    {
        Directory.CreateDirectory(_root);
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Test-Role", "Admin");
        using var body = Multipart(Encoding.UTF8.GetBytes(contents), name, mime);
        var response = await client.PostAsync("/api/admin/posts/image", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Storage_RejectsMismatchedMetadataTruncatedImagesAndExcessBytes()
    {
        var storage = Storage();
        var png = ImageBytes(MagickFormat.Png);
        foreach (var file in new[]
        {
            Form(png, "image.jpg", "image/jpeg"),
            Form(png, "image.png", "image/jpeg"),
            Form(png[..24], "image.png", "image/png"),
            Form([], "image.png", "image/png"),
            Form(new byte[ManagedImageStorage.MaximumFileBytes + 1], "image.png", "image/png")
        })
            await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(file, ImageCategory.Posts, default));
        Assert.False(Directory.Exists(Path.Combine(_root, "uploads")));
    }

    [Theory]
    [InlineData(4097, 1)]
    [InlineData(4001, 4000)]
    public async Task Storage_RejectsExcessDimensionsAndPixelCount(int width, int height)
    {
        // Build a valid compressed PNG independently of the globally limited decoder.
        var png = CreatePng(width, height);
        await Assert.ThrowsAsync<ArgumentException>(() => Storage().SaveAsync(Form(png, "large.png", "image/png"), ImageCategory.Posts, default));
        Assert.False(Directory.Exists(Path.Combine(_root, "uploads")));
    }

    [Fact]
    public async Task Storage_CancelledAndFailedWritesLeaveNoPublishedFilesAndReleaseGate()
    {
        var storage = Storage();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.SaveAsync(Form(ImageBytes(MagickFormat.Png), "a.png", "image/png"), ImageCategory.Posts, cancelled.Token));
        // A regular file prevents creation of the managed directory.
        Directory.CreateDirectory(_root);
        var blocker = Path.Combine(_root, "uploads");
        await File.WriteAllTextAsync(blocker, "existing data");
        await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync(Form(ImageBytes(MagickFormat.Png), "a.png", "image/png"), ImageCategory.Posts, default));
        Assert.Equal("existing data", await File.ReadAllTextAsync(blocker));
        File.Delete(blocker);
        var path = await storage.SaveAsync(Form(ImageBytes(MagickFormat.Png), "a.png", "image/png"), ImageCategory.Posts, default);
        Assert.True(File.Exists(Path.Combine(_root, path.TrimStart('/'))));
        Assert.Empty(Directory.GetFiles(_root, "*.uploading", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ConcurrentDecodeIsRejectedAndCancellationReleasesTheSlot()
    {
        var storage = Storage();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stream = new WaitingStream(entered);
        var waitingFile = new StreamFile(stream, 100);
        var first = storage.SaveAsync(waitingFile, ImageCategory.Posts, cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveAsync(
                Form(ImageBytes(MagickFormat.Png), "second.png", "image/png"), ImageCategory.Products, default));
        }
        finally { cancellation.Cancel(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        var path = await storage.SaveAsync(Form(ImageBytes(MagickFormat.Png), "retry.png", "image/png"), ImageCategory.Products, default);
        Assert.True(File.Exists(Path.Combine(_root, path.TrimStart('/'))));
    }

    [Fact]
    public async Task StorageBoundsActualBytesEvenIfDeclaredLengthIsSmaller()
    {
        using var stream = new MemoryStream(new byte[ManagedImageStorage.MaximumFileBytes + 1]);
        var file = new StreamFile(stream, 100);
        await Assert.ThrowsAsync<ArgumentException>(() => Storage().SaveAsync(file, ImageCategory.Posts, default));
        Assert.False(Directory.Exists(Path.Combine(_root, "uploads")));
    }

    [Fact]
    public async Task TemporaryFilesAreNotServed()
    {
        Directory.CreateDirectory(Path.Combine(_root, "uploads", "posts"));
        await File.WriteAllTextAsync(Path.Combine(_root, "uploads", "posts", "partial.uploading"), "partial");
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/uploads/posts/partial.uploading")).StatusCode);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateFactory()
        => new BiogardenWebApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseWebRoot(_root);
            builder.ConfigureTestServices(services => services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "ImageTest";
                options.DefaultChallengeScheme = "ImageTest";
                options.DefaultForbidScheme = "ImageTest";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthentication>("ImageTest", _ => { }));
        });

    private ManagedImageStorage Storage() => new(new TestEnvironment { WebRootPath = _root });
    private static FormFile Form(byte[] bytes, string name, string mime)
        => new(new MemoryStream(bytes), 0, bytes.Length, "image", name) { Headers = new HeaderDictionary(), ContentType = mime };
    private static MultipartFormDataContent Multipart(byte[] bytes, string name, string mime)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(mime);
        content.Add(file, "image", name);
        return content;
    }
    private static byte[] ImageBytes(MagickFormat format)
    {
        using var image = new MagickImage(MagickColors.Green, 16, 12);
        var profile = new ExifProfile();
        profile.SetValue(ExifTag.Artist, "Private metadata");
        image.SetProfile(profile);
        return image.ToByteArray(format);
    }
    private static byte[] CreatePng(int width, int height)
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // grayscale, one byte per pixel
        Chunk("IHDR", header);
        using var pixels = new MemoryStream();
        using (var compressed = new System.IO.Compression.ZLibStream(pixels, System.IO.Compression.CompressionLevel.SmallestSize, true))
        {
            var row = new byte[width + 1];
            for (var i = 0; i < height; i++) compressed.Write(row);
        }
        Chunk("IDAT", pixels.ToArray());
        Chunk("IEND", []);
        return output.ToArray();

        void Chunk(string type, byte[] data)
        {
            var number = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
            output.Write(number);
            var body = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
            output.Write(body);
            uint crc = 0xffffffff;
            foreach (var value in body)
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
            }
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(number, ~crc);
            output.Write(number);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
    private sealed record ImageResponse(string Path);
    private sealed class WaitingStream(TaskCompletionSource entered) : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }
    private sealed class StreamFile(Stream stream, long length) : IFormFile
    {
        public string ContentType => "image/png";
        public string ContentDisposition => string.Empty;
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length => length;
        public string Name => "image";
        public string FileName => "test.png";
        public Stream OpenReadStream() => stream;
        public void CopyTo(Stream target) => stream.CopyTo(target);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => stream.CopyToAsync(target, cancellationToken);
    }
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
    }
    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["Test-Role"].ToString();
            if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
