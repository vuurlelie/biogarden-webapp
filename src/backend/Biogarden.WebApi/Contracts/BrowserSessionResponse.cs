namespace Biogarden.WebApi.Contracts;

public sealed record BrowserSessionResponse(string UserId, string Email, string[] Roles, string SessionId, DateTimeOffset? ExpiresAtUtc);
