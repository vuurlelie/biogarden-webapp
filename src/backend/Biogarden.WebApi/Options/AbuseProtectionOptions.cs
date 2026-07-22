namespace Biogarden.WebApi.Options;

public sealed class AbuseProtectionOptions
{
    public const string SectionName = "AbuseProtection";

    public RateLimitRuleOptions Global { get; set; } = new()
    {
        PermitLimit = 120,
        WindowSeconds = 60
    };

    public RateLimitRuleOptions SensitiveAuth { get; set; } = new()
    {
        PermitLimit = 5,
        WindowSeconds = 60
    };

    public RateLimitRuleOptions PublicForm { get; set; } = new()
    {
        PermitLimit = 3,
        WindowSeconds = 60
    };

    public RateLimitRuleOptions Payment { get; set; } = new()
    {
        PermitLimit = 20,
        WindowSeconds = 60
    };

    public int RetryAfterSeconds { get; set; } = 60;

    public void ValidateForStartup()
    {
        ValidateRule(nameof(Global), Global);
        ValidateRule(nameof(SensitiveAuth), SensitiveAuth);
        ValidateRule(nameof(PublicForm), PublicForm);
        ValidateRule(nameof(Payment), Payment);

        if (RetryAfterSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:RetryAfterSeconds must be greater than 0.");
        }
    }

    private static void ValidateRule(string ruleName, RateLimitRuleOptions rule)
    {
        if (rule.PermitLimit <= 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{ruleName}:PermitLimit must be greater than 0.");
        }

        if (rule.WindowSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{ruleName}:WindowSeconds must be greater than 0.");
        }
    }
}

public sealed class RateLimitRuleOptions
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }
}
