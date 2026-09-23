namespace Biogarden.WebApi.Contracts;

public sealed class ApiErrorResponse
{
    public string Type { get; set; } = "about:blank";
    public string Title { get; set; } = "An error occurred.";
    public int Status { get; set; }
    public string? Detail { get; set; }
    public string? TraceId { get; set; }
    public string? Instance { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
}
