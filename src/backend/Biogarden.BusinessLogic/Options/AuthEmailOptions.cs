namespace Biogarden.BusinessLogic.Options;

public sealed class AuthEmailOptions
{
    public const string SectionName = "AuthEmail";

    public string ClientBaseUrl { get; init; } = "http://localhost:5173";
    public string ConfirmEmailPath { get; init; } = "/email-megerosites";
    public int UnconfirmedAccountRetentionDays { get; init; } = 14;
}
