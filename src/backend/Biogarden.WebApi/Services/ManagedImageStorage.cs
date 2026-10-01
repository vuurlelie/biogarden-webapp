using ImageMagick;

namespace Biogarden.WebApi.Services;

public enum ImageCategory { Posts, Products }

/// <summary>Validates and publishes immutable, server-named raster images.</summary>
public sealed class ManagedImageStorage(IWebHostEnvironment environment)
{
    public const long MaximumFileBytes = 5 * 1024 * 1024;
    // Leave room for multipart headers; the file itself is checked independently.
    public const long MaximumRequestBytes = MaximumFileBytes + 64 * 1024;
    public const uint MaximumDimension = 4096;
    public const ulong MaximumPixels = 16_000_000;
    public const string RetentionMessage = "A feltöltött képek törlése csak karbantartás során engedélyezett, hogy más mentés ne veszítse el a képét.";
    private static readonly SemaphoreSlim DecodeGate = new(1, 1);
    private static readonly Lazy<bool> DecoderLimits = new(() =>
    {
        ResourceLimits.Width = MaximumDimension;
        ResourceLimits.Height = MaximumDimension;
        ResourceLimits.Area = MaximumPixels;
        ResourceLimits.Memory = 256 * 1024 * 1024;
        ResourceLimits.Disk = 0;
        // The codec uses temporary image buffers even for a single decoded frame.
        ResourceLimits.ListLength = 8;
        ResourceLimits.Thread = 1;
        ResourceLimits.MaxProfileSize = 1024 * 1024;
        return true;
    });

    public async Task<string> SaveAsync(IFormFile file, ImageCategory category, CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > MaximumFileBytes)
            throw new ArgumentException("A kép mérete 1 bájt és 5 MB között lehet.");
        var (format, extension, contentType) = Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => (MagickFormat.Jpeg, ".jpg", "image/jpeg"),
            ".png" => (MagickFormat.Png, ".png", "image/png"),
            ".webp" => (MagickFormat.WebP, ".webp", "image/webp"),
            _ => throw new ArgumentException("Csak JPG, PNG vagy WEBP kép tölthető fel.")
        };
        if (!string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A kép típusa és kiterjesztése nem egyezik.");

        // Reject excess concurrent decoding instead of accumulating large queued requests.
        if (!await DecodeGate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("Másik képfeltöltés folyamatban van. Próbáld újra.");
        string? temporaryPath = null;
        try
        {
            await using var input = file.OpenReadStream();
            using var bytes = new MemoryStream();
            var buffer = new byte[64 * 1024];
            int count;
            while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (bytes.Length + count > MaximumFileBytes)
                    throw new ArgumentException("A kép mérete legfeljebb 5 MB lehet.");
                bytes.Write(buffer, 0, count);
            }
            var data = bytes.ToArray();
            if (!HasSignature(data, format))
                throw new ArgumentException("A fájl nem a megadott típusú kép.");

            _ = DecoderLimits.Value;
            var settings = new MagickReadSettings { Format = format, FrameCount = 1 };
            using var decoded = new MagickImage();
            var hasWarning = false;
            decoded.Warning += (_, _) => hasWarning = true;
            decoded.Ping(data, settings);
            if (decoded.Width == 0 || decoded.Height == 0 || decoded.Width > MaximumDimension ||
                decoded.Height > MaximumDimension || (ulong)decoded.Width * decoded.Height > MaximumPixels)
                throw new ArgumentException("A kép legfeljebb 4096 × 4096 képpont és 16 megapixel lehet.");
            decoded.Read(data, settings);
            if (hasWarning) throw new ArgumentException("A kép sérült vagy nem teljes.");
            decoded.AutoOrient();
            decoded.Strip();

            var directory = GetDirectory(category);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            // The temporary suffix is not served by the static-file middleware. A rename in
            // the same directory publishes only the complete, re-encoded image atomically.
            temporaryPath = Path.Combine(directory, $"{Guid.NewGuid():N}.uploading");
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await decoded.WriteAsync(output, format, cancellationToken);
                if (output.Length > MaximumFileBytes)
                    throw new ArgumentException("A feldolgozott kép túl nagy. Használj kisebb képet.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, Path.Combine(directory, fileName));
            temporaryPath = null;
            return $"/uploads/{Folder(category)}/{fileName}";
        }
        catch (MagickException exception)
        {
            throw new ArgumentException("A kép sérült, nem támogatott vagy túl nagy felbontású.", exception);
        }
        finally
        {
            try { if (temporaryPath is not null) File.Delete(temporaryPath); }
            finally { DecodeGate.Release(); }
        }
    }

    private string GetDirectory(ImageCategory category)
    {
        var root = Path.GetFullPath(environment.WebRootPath);
        var uploads = Path.Combine(root, "uploads");
        var target = Path.Combine(uploads, Folder(category));
        // Reject junctions/symlinks within the managed tree rather than writing elsewhere.
        foreach (var directory in new[] { root, uploads, target })
        {
            Directory.CreateDirectory(directory);
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("A képtároló útvonala nem használhat szimbolikus hivatkozást.");
        }
        return target;
    }

    private static string Folder(ImageCategory category) => category switch
    {
        ImageCategory.Posts => "posts",
        ImageCategory.Products => "products",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    private static bool HasSignature(byte[] data, MagickFormat format) => format switch
    {
        MagickFormat.Jpeg => data.Length >= 3 && data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff,
        MagickFormat.Png => data.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        MagickFormat.WebP => data.Length >= 12 && data.AsSpan(0, 4).SequenceEqual("RIFF"u8) && data.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
}
