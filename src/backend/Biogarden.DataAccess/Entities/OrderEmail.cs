namespace Biogarden.DataAccess.Entities;

public sealed class OrderEmail
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? HtmlBody { get; set; }
    public string? PlainTextBody { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public int Attempts { get; set; }
}
