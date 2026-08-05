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
/// **Vlastita baza, nikad razvojna.** Testovi brišu tabele između slučajeva. Dok su gađali
/// istu bazu koju koristi aplikacija, jedan `dotnet test` je obrisao svu prikupljenu
/// historiju — a AVP Sava ne objavljuje arhivu, pa se izgubljena mjerenja ne mogu vratiti.
/// Zato baza za testove ima svoje ime, pravi se sama, i brisanje je zaključano provjerom
/// tog imena.
///
/// Prije pokretanja: `docker compose up -d`.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>Sufiks bez kojeg se ništa ne briše. Zaštita od ponavljanja iste greške.</summary>
    private const string RequiredSuffix = "_test";

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("VODOSTAJI_TEST_CONNECTION");

        if (configured is { Length: > 0 })
        {
            ConnectionString = configured;
        }
        else
        {
            var builder = new NpgsqlConnectionStringBuilder(
                DesignTimeDbContextFactory.LocalDevelopmentConnection)
            {
                Database = "vodostaji_test",
            };

            ConnectionString = builder.ConnectionString;
        }

        var database = new NpgsqlConnectionStringBuilder(ConnectionString).Database ?? "";

        if (!database.EndsWith(RequiredSuffix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Baza za testove mora imati ime koje završava na `{RequiredSuffix}`; dobijeno "
                + $"`{database}`. Ovi testovi brišu tabele, a jednom su tako obrisali svu "
                + "prikupljenu historiju iz razvojne baze. Izvor ne objavljuje arhivu, pa se "
                + "takav gubitak ne može popraviti.");
        }

        await EnsureDatabaseExistsAsync(database);

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

    private async Task EnsureDatabaseExistsAsync(string database)
    {
        // Povezujemo se na `postgres` bazu samo da bismo napravili testnu ako je nema.
        var admin = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" };

        try
        {
            await using var connection = new NpgsqlConnection(admin.ConnectionString);
            await connection.OpenAsync();

            await using var exists = new NpgsqlCommand(
                "SELECT 1 FROM pg_database WHERE datname = @name", connection);
            exists.Parameters.AddWithValue("name", database);

            if (await exists.ExecuteScalarAsync() is not null)
            {
                return;
            }

            // Ime dolazi iz našeg koda ili iz varijable okruženja koju postavlja onaj ko
            // pokreće testove; svejedno ide kroz identifikator u navodnicima.
            await using var create = new NpgsqlCommand(
                $"CREATE DATABASE \"{database.Replace("\"", "\"\"", StringComparison.Ordinal)}\"",
                connection);

            await create.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Baza za testove nije dostupna. Pokreni `docker compose up -d`. "
                + "Ovi testovi namjerno ne rade protiv in-memory baze — provjeravaju check "
                + "constraint-e koje samo Postgres izvršava.",
                ex);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
