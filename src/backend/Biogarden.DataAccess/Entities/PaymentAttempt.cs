namespace Biogarden.DataAccess.Entities;

public sealed class PaymentAttempt
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? CheckoutId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool CreateRequested { get; set; }
    public string Status { get; set; } = "CREATING";
    public byte[] RowVersion { get; set; } = [];
}
