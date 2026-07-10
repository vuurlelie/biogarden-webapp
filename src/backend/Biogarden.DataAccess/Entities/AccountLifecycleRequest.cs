using Biogarden.DataAccess.Enums;

namespace Biogarden.DataAccess.Entities;

public class AccountLifecycleRequest
{
    public int Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public AccountLifecycleRequestType Type { get; set; }

    public AccountLifecycleRequestStatus Status { get; set; } = AccountLifecycleRequestStatus.Pending;

    public string? Reason { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? HandledAtUtc { get; set; }

    public Guid? HandledByAdminId { get; set; }
    public ApplicationUser? HandledByAdmin { get; set; }

    public string? AdminNote { get; set; }
}
