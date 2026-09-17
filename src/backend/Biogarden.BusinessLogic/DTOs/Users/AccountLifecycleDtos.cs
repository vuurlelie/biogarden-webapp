using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Users;

public sealed class AccountLifecycleRequestDto
{
    public AccountLifecycleRequestType Type { get; set; }

    public string? Reason { get; set; }
}

public sealed class AccountLifecycleResultDto
{
    public string Message { get; init; } = string.Empty;

    public bool SessionEnded { get; init; }

    public AccountLifecycleRequestStatus Status { get; init; }
}
