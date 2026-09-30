namespace Biogarden.BusinessLogic.Options;

public sealed class GlsOptions
{
    public const string SectionName = "Gls";
    public const string HttpClientName = "GlsDirectory";
    public string DirectoryUrl { get; init; } = "https://map.gls-hungary.com/data/deliveryPoints/hu.json";
    public string CountryCode { get; init; } = "HU";
    public int CacheMinutes { get; init; } = 15;
    public int TimeoutSeconds { get; init; } = 10;
    public int RetryDelaySeconds { get; init; } = 30;
    public int MaximumResponseBytes { get; init; } = 8 * 1024 * 1024;
    public void ValidateForStartup()
    {
        if (!Uri.TryCreate(DirectoryUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !string.IsNullOrEmpty(uri.UserInfo) || CountryCode != "HU" ||
            CacheMinutes is < 1 or > 60 || TimeoutSeconds is < 1 or > 30 ||
            RetryDelaySeconds is < 1 or > 300 || MaximumResponseBytes is < 1024 or > 16 * 1024 * 1024)
            throw new InvalidOperationException("Gls configuration is invalid.");
    }
}
