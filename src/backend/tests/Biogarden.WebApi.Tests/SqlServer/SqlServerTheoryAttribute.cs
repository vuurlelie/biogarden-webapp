namespace Biogarden.WebApi.Tests.SqlServer;

public sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerFactAttribute.ConnectionVariable)))
            Skip = "Set BIOGARDEN_TEST_SQLSERVER to a disposable SQL Server instance to run database integration tests.";
    }
}
