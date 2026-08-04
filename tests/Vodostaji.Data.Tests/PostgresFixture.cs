using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vodostaji.Data;

namespace Vodostaji.Data.Tests;

/// <summary>
/// Ovi testovi traže **stvarni Postgres**, ne in-memory zamjenu.
///
/// Razlog je konkretan: dvije najvažnije garancije sheme su check constraint-i, a njih
/// in-memory provider uopšte ne izvršava. Test koji ih ne bi provjerio protiv prave baze
/// tvrdio bi da pravilo drži, a ne bi ga ni dotakao.
///
/// Prije pokretanja: `docker compose up -d`.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("VODOSTAJI_CONNECTION")
        ?? DesignTimeDbContextFactory.LocalDevelopmentConnection;

    public async Task InitializeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Baza nije dostupna. Pokreni `docker compose up -d` pa `dotnet ef database update "
                + "-p src/Vodostaji.Data -s src/Vodostaji.Data`. "
                + "Ovi testovi namjerno ne rade protiv in-memory baze — provjeravaju check "
                + "constraint-e koje samo Postgres izvršava.",
                ex);
        }

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public VodostajiDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<VodostajiDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    /// <summary>Svaki test kreće od prazne baze — redoslijed testova ne smije ništa značiti.</summary>
    public async Task ResetAsync()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE station_states, stations, measurements RESTART IDENTITY;");
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
