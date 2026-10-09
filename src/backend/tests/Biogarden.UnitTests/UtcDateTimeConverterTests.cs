using Biogarden.DataAccess;

namespace Biogarden.UnitTests;

public sealed class UtcDateTimeConverterTests
{
    [Fact]
    public void DatabaseTimestampIsMarkedUtcWithoutChangingItsValue()
    {
        var converter = new UtcDateTimeConverter();
        var stored = new DateTime(2026, 10, 9, 5, 12, 0, DateTimeKind.Unspecified);
        var result = converter.ConvertFromProviderExpression.Compile()(stored);
        Assert.Equal(stored.Ticks, result.Ticks);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.EndsWith("Z", result.ToString("O"));
        Assert.Equal(stored.Ticks, converter.ConvertToProviderExpression.Compile()(result).Ticks);
    }
}
