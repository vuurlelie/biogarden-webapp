using System.ComponentModel.DataAnnotations;
using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Users;

public sealed class AccountLifecycleRequestDto
{
    [EnumDataType(typeof(AccountLifecycleRequestType))]
    public AccountLifecycleRequestType Type { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }
}

public sealed class AccountLifecycleResultDto
{
    public string Message { get; init; } = string.Empty;

    public bool SessionEnded { get; init; }

    public AccountLifecycleRequestStatus Status { get; init; }
}
