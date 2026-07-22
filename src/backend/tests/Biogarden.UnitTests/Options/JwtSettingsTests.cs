using Biogarden.BusinessLogic.Options;

namespace Biogarden.UnitTests.Configuration;

public sealed class JwtSettingsTests
{
    [Fact]
    public void ValidateForStartup_WhenSettingsAreValid_DoesNotThrow()
    {
        var options = new JwtSettings
        {
            Issuer = "BiogardenApi",
            Audience = "BiogardenClient",
            SecretKey = "ThisIsARandomEnoughJwtSecretForTests123",
            ExpirationMinutes = 60
        };

        options.ValidateForStartup();
    }

    [Theory]
    [InlineData("")]
    [InlineData("short-secret")]
    public void ValidateForStartup_WhenSecretKeyIsMissingOrTooShort_Throws(
        string secretKey)
    {
        var options = new JwtSettings
        {
            Issuer = "BiogardenApi",
            Audience = "BiogardenClient",
            SecretKey = secretKey,
            ExpirationMinutes = 60
        };

        Assert.Throws<InvalidOperationException>(options.ValidateForStartup);
    }
}
