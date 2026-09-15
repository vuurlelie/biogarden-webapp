namespace Biogarden.WebApi.Tests.SqlServer;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string ConnectionVariable = "BIOGARDEN_TEST_SQLSERVER";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
        {
            Skip = "Set BIOGARDEN_TEST_SQLSERVER to a disposable SQL Server instance to run database integration tests.";
        }
    }
}
