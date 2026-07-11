namespace Biogarden.BusinessLogic.Options;

public sealed class AuthEmailOptions
{
    public const string SectionName = "AuthEmail";

    public string ClientBaseUrl { get; init; } = "http://localhost:5173";
    public string ConfirmEmailPath { get; init; } = "/email-megerosites";
    public string ResetPasswordPath { get; init; } = "/jelszo-visszaallitas";
    public int UnconfirmedAccountRetentionDays { get; init; } = 14;

    public void ValidateForStartup()
    {
        if (!Uri.TryCreate(ClientBaseUrl, UriKind.Absolute, out var clientBaseUri)
            || (clientBaseUri.Scheme != Uri.UriSchemeHttp && clientBaseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "AuthEmail:ClientBaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (!ConfirmEmailPath.StartsWith('/'))
        {
            throw new InvalidOperationException(
                "AuthEmail:ConfirmEmailPath must start with '/'.");
        }

        if (!ResetPasswordPath.StartsWith('/'))
        {
            throw new InvalidOperationException(
                "AuthEmail:ResetPasswordPath must start with '/'.");
        }

        if (UnconfirmedAccountRetentionDays <= 0)
        {
            throw new InvalidOperationException(
                "AuthEmail:UnconfirmedAccountRetentionDays must be greater than zero.");
        }
    }
}
