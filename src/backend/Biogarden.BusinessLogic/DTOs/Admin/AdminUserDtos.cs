using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Admin;

public sealed class AdminUserDto
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? PhoneNumber { get; init; }

    public string? CompanyName { get; init; }

    public string? TaxNumber { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public bool IsAdmin { get; init; }

    public bool EmailConfirmed { get; init; }

    public bool IsDeactivated { get; init; }

    public bool IsAnonymized { get; init; }

    public DateTime CreatedAt { get; init; }

    public int OrderCount { get; init; }

    public int ActiveOrderCount { get; init; }

    public AdminAccountLifecycleRequestDto? PendingRequest { get; init; }
}

public sealed class AdminAccountLifecycleRequestDto
{
    public int Id { get; init; }

    public AccountLifecycleRequestType Type { get; init; }

    public AccountLifecycleRequestStatus Status { get; init; }

    public string? Reason { get; init; }

    public string Message { get; init; } = string.Empty;

    public DateTime RequestedAtUtc { get; init; }
}

public sealed class AdminAccountLifecycleDecisionDto
{

    public string? AdminNote { get; set; }
}

public sealed class AdminUserErasureResultDto
{
    public Guid UserId { get; init; }

    public AdminUserDto? User { get; init; }

    public string Message { get; init; } = string.Empty;
}
