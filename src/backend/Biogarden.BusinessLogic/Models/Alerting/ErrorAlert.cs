using Microsoft.Extensions.Logging;

namespace Biogarden.BusinessLogic.Models.Alerting;

public sealed record ErrorAlert
{
    public required string AlertKey { get; init; }
    public required string Source { get; init; }
    public required string Summary { get; init; }
    public LogLevel Level { get; init; } = LogLevel.Error;
    public string? Detail { get; init; }
    public Exception? Exception { get; init; }
}
