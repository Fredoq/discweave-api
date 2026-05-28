using Npgsql;
using Testcontainers.PostgreSql;

namespace Cratebase.Api.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("cratebase")
        .WithUsername("cratebase")
        .WithPassword("cratebase")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task<string> CreateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        string databaseName = $"cratebase_{Guid.CreateVersion7():N}";

        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"""CREATE DATABASE "{databaseName}";""";
        _ = await command.ExecuteNonQueryAsync(cancellationToken);

        return new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = databaseName
        }.ConnectionString;
    }
}
