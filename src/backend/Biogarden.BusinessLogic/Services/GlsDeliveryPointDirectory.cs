using System.Text.Json;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess.Enums;
using Microsoft.Extensions.Options;

namespace Biogarden.BusinessLogic.Services;

// Singleton: one immutable snapshot and one refresh at a time per API process.
public sealed class GlsDeliveryPointDirectory(Func<HttpClient> createClient, IOptions<GlsOptions> options, TimeProvider clock)
    : IGlsDeliveryPointDirectory, IDisposable
{
    private readonly GlsOptions _options = options.Value;
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private Cache? _cache;
    private DateTimeOffset _retryAfter;
    private sealed record Cache(IReadOnlyDictionary<string, GlsDeliveryPointDto> Points, DateTimeOffset Expires);

    public async Task<GlsDeliveryPointDto> ResolveAsync(string id, FulfillmentMethod type, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 100 ||
            type is not (FulfillmentMethod.ParcelLocker or FulfillmentMethod.ParcelShop))
            throw new ArgumentException("Válassz egy GLS átvételi pontot.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        try
        {
            var cache = Volatile.Read(ref _cache);
            if (cache is null || cache.Expires <= clock.GetUtcNow())
            {
                await _refresh.WaitAsync(timeout.Token);
                try
                {
                    cache = _cache;
                    if (cache is null || cache.Expires <= clock.GetUtcNow())
                    {
                        if (_retryAfter > clock.GetUtcNow()) throw new GlsDirectoryUnavailableException();
                        try
                        {
                            var points = await DownloadAsync(timeout.Token);
                            cache = new(points, clock.GetUtcNow().AddMinutes(_options.CacheMinutes));
                            Volatile.Write(ref _cache, cache);
                        }
                        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or OperationCanceledException)
                        {
                            if (cancellationToken.IsCancellationRequested) throw;
                            _retryAfter = clock.GetUtcNow().AddSeconds(_options.RetryDelaySeconds);
                            throw new GlsDirectoryUnavailableException();
                        }
                    }
                }
                finally { _refresh.Release(); }
            }
            if (!cache.Points.TryGetValue(id, out var point) || point.Type != type)
                throw new InvalidOperationException("A kiválasztott GLS átvételi pont nem elérhető ehhez az átvételi módhoz. Válassz új pontot.");
            return point;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GlsDirectoryUnavailableException();
        }
    }

    private async Task<IReadOnlyDictionary<string, GlsDeliveryPointDto>> DownloadAsync(CancellationToken ct)
    {
        using var client = createClient();
        using var response = await client.GetAsync(_options.DirectoryUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > _options.MaximumResponseBytes) throw new JsonException("Directory too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > _options.MaximumResponseBytes) throw new JsonException("Directory too large.");
            buffer.Write(chunk, 0, read);
        }
        using var json = JsonDocument.Parse(buffer.ToArray());
        if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            throw new JsonException("Unexpected GLS directory format.");
        var points = new Dictionary<string, GlsDeliveryPointDto>(StringComparer.Ordinal);
        foreach (var item in items.EnumerateArray())
        {
            var type = Text(item, "type") switch
            {
                "parcel-locker" => FulfillmentMethod.ParcelLocker,
                "parcel-shop" => FulfillmentMethod.ParcelShop,
                _ => (FulfillmentMethod?)null
            };
            if (type is null || !item.TryGetProperty("contact", out var contact) ||
                Text(contact, "countryCode") != _options.CountryCode ||
                !item.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array) continue;
            var featureSet = features.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()).ToHashSet();
            if (!featureSet.Contains("delivery")) continue;
            var id = Text(item, "id"); var name = Text(item, "name");
            var postal = Text(contact, "postalCode"); var city = Text(contact, "city"); var address = Text(contact, "address");
            if (!Valid(id, 100) || !Valid(name, 200) || !Valid(postal, 20) || !Valid(city, 100) || !Valid(address, 200)) continue;
            if (!points.TryAdd(id!, new(id!, type.Value, name!, _options.CountryCode, postal!, city!, address!,
                featureSet.Contains("acceptsCash"), featureSet.Contains("acceptsCard"))))
                throw new JsonException("Duplicate GLS point identity.");
        }
        if (points.Count == 0) throw new JsonException("No usable GLS delivery points.");
        return points;
    }

    private static string? Text(JsonElement item, string name) => item.ValueKind == JsonValueKind.Object &&
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool Valid(string? value, int length) => !string.IsNullOrWhiteSpace(value) && value.Length <= length;
    public void Dispose() => _refresh.Dispose();
}
