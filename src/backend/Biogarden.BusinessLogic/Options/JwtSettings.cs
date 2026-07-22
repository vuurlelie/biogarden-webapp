namespace Biogarden.BusinessLogic.Options;

public class JwtSettings
{
    public const string SectionName = "Jwt";
    private const int MinimumSecretKeyLength = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; }

    public void ValidateForStartup()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException(
                "Jwt:Issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException(
                "Jwt:Audience is not configured.");
        }

        if (string.IsNullOrWhiteSpace(SecretKey) ||
            SecretKey.Length < MinimumSecretKeyLength)
        {
            throw new InvalidOperationException(
                $"Jwt:SecretKey must be configured outside source control and contain at least {MinimumSecretKeyLength} characters.");
        }

        if (ExpirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                "Jwt:ExpirationMinutes must be greater than 0.");
        }
    }
}
