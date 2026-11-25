using Acme.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Acme.IntegrationTests.TestSupport;

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container;

    public string ServerConnectionString { get; private set; }
    public string Unavailable { get; private set; }

    public async Task InitializeAsync()
    {
        ServerConnectionString = Environment.GetEnvironmentVariable(IntegrationConstants.PostgresVariable);

        if (ServerConnectionString == IntegrationConstants.SkipValue)
        {
            Unavailable = $"{IntegrationConstants.PostgresVariable} is {IntegrationConstants.SkipValue}.";
            return;
        }

        if (!string.IsNullOrEmpty(ServerConnectionString))
        {
            return;
        }

        try
        {
            _container = new PostgreSqlBuilder(IntegrationConstants.PostgresImage).Build();
            await _container.StartAsync();
            ServerConnectionString = _container.GetConnectionString();
        }
        catch (Exception exception) when (exception is DockerUnavailableException or HttpRequestException or TimeoutException or ArgumentException)
        {
            Unavailable = $"No PostgreSQL: set {IntegrationConstants.PostgresVariable} or start Docker ({exception.GetType().Name}).";
        }
    }

    public async Task<string> CreateDatabaseAsync(string testClass)
    {
        var database = $"{IntegrationConstants.DatabasePrefix}{testClass.ToLowerInvariant()}_{Guid.CreateVersion7():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(ServerConnectionString) { Database = database }.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options;

        await using var appDbContext = new AppDbContext(options);
        await appDbContext.Database.MigrateAsync();

        return connectionString;
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
