namespace Biogarden.BusinessLogic.Options;

public sealed class UnconfirmedAccountCleanupOptions
{
    public const string SectionName = "UnconfirmedAccountCleanup";
    public bool Enabled { get; init; } = true;
    public int BatchSize { get; init; } = 100;
    public int IntervalMinutes { get; init; } = 60;

    public void ValidateForStartup()
    {
        if (BatchSize is < 1 or > 1000 || IntervalMinutes is < 1 or > 1440)
            throw new InvalidOperationException("UnconfirmedAccountCleanup requires BatchSize 1–1000 and IntervalMinutes 1–1440.");
    }
}
