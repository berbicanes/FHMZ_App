using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vodostaji.Data;

/// <summary>
/// Omogućava `dotnet ef` dok `Vodostaji.Api` još ne postoji. Kad Api dođe, migracije se
/// mogu praviti i preko njega, kako stoji u komandama u `CLAUDE.md`.
///
/// Connection string se čita iz `VODOSTAJI_CONNECTION`, a lokalni razvojni je rezerva —
/// nikad se ne koristi izvan `docker compose` okruženja iz ovog repozitorija.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VodostajiDbContext>
{
    public const string LocalDevelopmentConnection =
        "Host=localhost;Port=5432;Database=vodostaji;Username=vodostaji;Password=vodostaji-local-dev";

    public VodostajiDbContext CreateDbContext(string[] args)
    {
        var connection =
            Environment.GetEnvironmentVariable("VODOSTAJI_CONNECTION") ?? LocalDevelopmentConnection;

        var options = new DbContextOptionsBuilder<VodostajiDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new VodostajiDbContext(options);
    }
}
