namespace Biogarden.WebApi.Contracts;

public sealed class ApiErrorResponse
{
    public string Title { get; set; } = "An error occurred.";
    public int Status { get; set; }
    public string? Detail { get; set; }
    public string? TraceId { get; set; }
}
