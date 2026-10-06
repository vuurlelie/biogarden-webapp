namespace Biogarden.BusinessLogic.Options;

public sealed class OperationalMonitoringOptions
{
    public const string SectionName = "OperationalMonitoring";
    public int EmailFailureAttempts { get; init; } = 5;
    public int EmailOverdueMinutes { get; init; } = 30;
    public int PaymentRecoveryMinutes { get; init; } = 15;

    public void ValidateForStartup()
    {
        if (EmailFailureAttempts is < 1 or > 100 || EmailOverdueMinutes is < 1 or > 10080 || PaymentRecoveryMinutes is < 1 or > 10080)
            throw new InvalidOperationException("Operational monitoring thresholds must be positive: up to 100 attempts or 10080 minutes.");
    }
}
