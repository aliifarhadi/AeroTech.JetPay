using System.Security.Claims;
using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AeroTech.JetPay.Application.AcceptanceTests.Fixtures;

/// <summary>A throw-away database built from the real migrations on the development SQL Server.</summary>
public sealed class TestDatabase(IClock clock) : IAsyncDisposable
{
    private readonly string _connectionString = new SqlConnectionStringBuilder(DevelopmentConnectionString())
    {
        InitialCatalog = $"DotAirJetPay_Tests_{Guid.NewGuid():N}"
    }.ConnectionString;

    public JetPayDbContext NewContext()
        => new(
            new DbContextOptionsBuilder<JetPayDbContext>()
                .UseSqlServer(_connectionString, sql => sql.MigrationsHistoryTable(JetPayDbContext.MigrationsHistoryTable, JetPayDbContext.MigrationsHistorySchema))
                .Options,
            new AnonymousIdentityService(),
            clock,
            new IgnoringDomainEventDispatcher());

    public async Task MigrateAsync()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = NewContext();
        await context.Database.EnsureDeletedAsync();
    }

    private static string DevelopmentConnectionString()
        => new ConfigurationBuilder()
               .AddJsonFile(Path.Combine(RepositoryRoot(), "src", "AeroTech.JetPay.ServiceHost", "appsettings.Development.json"))
               .Build()
               .GetConnectionString("CommandDbContext")
           ?? throw new InvalidOperationException("ConnectionStrings:CommandDbContext is not configured.");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AeroTech.JetPay.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("AeroTech.JetPay.sln was not found above the test output.");
    }

    private sealed class AnonymousIdentityService : IIdentityService
    {
        public long? CurrentUserId => null;

        public long? CurrentCustomerId => null;

        public long RequiredCurrentUserId => throw new InvalidOperationException("No user in tests.");

        public Guid RequiredDeviceId => throw new InvalidOperationException("No device in tests.");

        public bool IsAuthenticated => false;

        public List<Claim>? Claims => null;

        public void CheckAccess(string scopeType, object scopeId)
        {
        }
    }

    private sealed class IgnoringDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
