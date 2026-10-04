namespace Biogarden.BusinessLogic.Options;

public sealed class CartCleanupOptions
{
    public const string SectionName = "CartCleanup";
    public bool Enabled { get; init; } = true;
    public int RetentionDays { get; init; } = 7;
    public int BatchSize { get; init; } = 100;
    public int IntervalMinutes { get; init; } = 60;

    public void ValidateForStartup()
    {
        if (RetentionDays is < 1 or > 365 || BatchSize is < 1 or > 1000 || IntervalMinutes is < 1 or > 1440)
            throw new InvalidOperationException("CartCleanup requires RetentionDays 1–365, BatchSize 1–1000 and IntervalMinutes 1–1440.");
    }
}
