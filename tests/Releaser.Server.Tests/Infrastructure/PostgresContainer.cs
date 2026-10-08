using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Releaser.Server.Tests.Infrastructure.PostgresContainer))]

namespace Releaser.Server.Tests.Infrastructure;

/// <summary>One PostgreSQL 18 container per test run; every test gets its own database.</summary>
public sealed class PostgresContainer : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18.6-alpine").WithCommand("-c", "max_connections=1000").Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public async Task<string> CreateDatabaseAsync()
    {
        var name = "test_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name, MaxPoolSize = 30 }.ConnectionString;
    }
}
