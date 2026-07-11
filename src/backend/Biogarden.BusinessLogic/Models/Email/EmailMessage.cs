namespace Biogarden.BusinessLogic.Models.Email;

public sealed record EmailMessage
{
    public required string To { get; init; }
    public required string Subject { get; init; }
    public string? PlainTextBody { get; init; }
    public string? HtmlBody { get; init; }
}
