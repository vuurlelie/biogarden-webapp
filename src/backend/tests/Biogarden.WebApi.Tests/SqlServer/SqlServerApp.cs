using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.DataAccess;
using Biogarden.DataAccess.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biogarden.WebApi.Tests.SqlServer;

// Every test owns a fresh database. Never derive its name from an application config file.
public sealed class SqlServerApp : IAsyncLifetime
{
    private readonly string _databaseName = $"BiogardenIntegration_{Guid.NewGuid():N}";
    private string _connectionString = string.Empty;
    private bool _created;
    public TestApi Factory { get; private set; } = null!;
    public TestEmailSender Sender { get; } = new();
    public QueueSaveFailure QueueFailure { get; } = new();
    public ConcurrentSaveGate SaveGate { get; } = new();

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable(SqlServerFactAttribute.ConnectionVariable)
            ?? throw new InvalidOperationException("SQL Server test connection is required.");
        var builder = new SqlConnectionStringBuilder(supplied)
        {
            InitialCatalog = _databaseName,
            Pooling = false
        };
        _connectionString = builder.ConnectionString;
        await using var database = OpenDatabase();
        // Create only a database which does not already exist. EnsureCreated would bypass migrations.
        var creator = database.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
        if (await creator.ExistsAsync()) throw new InvalidOperationException("Test database already exists.");
        await creator.CreateAsync();
        _created = true;
        try
        {
            await database.Database.MigrateAsync();
            Factory = new TestApi(_connectionString, Sender, QueueFailure, SaveGate);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public AppDbContext OpenDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(_connectionString).Options);

    public async Task RestartApiAsync()
    {
        await Factory.DisposeAsync();
        Factory = new TestApi(_connectionString, Sender, QueueFailure, SaveGate);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        if (!_created) return;

        var target = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
        if (target != _databaseName || !target.StartsWith("BiogardenIntegration_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to delete a database not owned by this test.");
        await using var database = OpenDatabase();
        await database.Database.EnsureDeletedAsync();
        _created = false;
    }

    public sealed class TestApi(string connection, TestEmailSender sender, QueueSaveFailure failure, ConcurrentSaveGate saveGate)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:SecretKey", "SqlServerIntegrationTestsOnlySecretKey123456789");
            builder.UseSetting("Jwt:Issuer", "integration-tests");
            builder.UseSetting("Jwt:Audience", "integration-tests");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connection);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connection,
                    ["Jwt:SecretKey"] = "SqlServerIntegrationTestsOnlySecretKey123456789",
                    ["Jwt:Issuer"] = "integration-tests",
                    ["Jwt:Audience"] = "integration-tests",
                    ["Jwt:ExpirationMinutes"] = "30",
                    ["Email:Enabled"] = "false",
                    ["SumUp:Enabled"] = "false",
                    ["ErrorAlerts:Enabled"] = "false",
                    ["OrderEmailOutbox:Enabled"] = "false",
                    ["EmailBranding:ContactEmail"] = "business@example.com",
                    ["AbuseProtection:PublicForm:PermitLimit"] = "100"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddSingleton<IEmailService>(sender);
                services.AddDbContext<AppDbContext>(options => options.AddInterceptors(failure, saveGate));
            });
        }
    }

    // Only external mail transport is replaced. Templates, queue, dispatcher and SQL are real.
    public sealed class TestEmailSender : IEmailService
    {
        public bool Fail { get; set; }
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new System.Net.Mail.SmtpException("Simulated outage");
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    public sealed class QueueSaveFailure : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<OrderEmail>()
                .Any(entry => entry.State == EntityState.Added))
                throw new InvalidOperationException("Simulated queue persistence failure");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    // Force both requests past their reads before either writes. No sleeps or mocked persistence.
    public sealed class ConcurrentSaveGate : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _bothArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public Type? EntityType { get; set; }
        public EntityState State { get; set; } = EntityState.Added;
        public int Arrivals => Volatile.Read(ref _arrivals);
        public Task WaitForBothAsync() => _bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(20));
        public void Release() => _release.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (EntityType is not null && eventData.Context!.ChangeTracker.Entries()
                .Any(entry => entry.State == State && entry.Entity.GetType() == EntityType))
            {
                var arrival = Interlocked.Increment(ref _arrivals);
                if (arrival == 2) _bothArrived.TrySetResult();
                if (arrival <= 2) await _release.Task.WaitAsync(TimeSpan.FromSeconds(25), cancellationToken);
            }
            return result;
        }
    }
}
