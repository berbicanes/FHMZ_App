using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vodostaji.Core;
using Vodostaji.Data;

namespace Vodostaji.Data.Tests;

/// <summary>
/// Skladištenje ostalih mjerenja — proticaj, temperatura vode, podzemne vode.
///
/// Ista pravila kao za vodostaj: ingest je idempotentan, i shema sama brani ono što se u
/// kodu može zaboraviti. Ovo su testovi protiv **stvarnog Postgresa**, jer CHECK i
/// jedinstveni indeks ne postoje u memoriji.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ObservationStoreTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Fetched = new(2026, 8, 8, 21, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Measured = new(2026, 8, 8, 19, 0, 0, TimeSpan.Zero);

    private static readonly Attribution Sava = new()
    {
        AgencyName = "Agencija za vodno područje rijeke Save",
        AgencyUrl = new Uri("https://www.voda.ba"),
    };

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Station Station() => new()
    {
        SourceId = "avp-sava-wiski",
        StationKey = "9019",
        Name = "HS Bliha",
        River = "Bliha",
        ExpectedInterval = TimeSpan.FromHours(1),
        Attribution = Sava,
    };

    private static Observation Observation(
        ObservationParameter parameter,
        decimal value,
        string unit,
        DateTimeOffset? measuredAt = null) => new()
        {
            Parameter = parameter,
            ParameterLabelOriginal = parameter.ToString(),
            Value = value,
            Unit = unit,
            MeasuredAt = measuredAt ?? Measured,
        };

    private async Task SaveAsync(params StationReading[] readings)
    {
        await using var context = postgres.CreateContext();

        await new EfReadingStore(context).SaveAsync(
            new SourceFetchResult
            {
                SourceId = "avp-sava-wiski",
                FetchedAt = Fetched,
                Readings = readings,
            },
            CancellationToken.None);
    }

    private static StationReading.Measured Reading(params Observation[] observations) => new()
    {
        Station = Station(),
        StatusLabelOriginal = "#MIN#",
        ClaimedLevel = AlertLevel.Unknown,
        MeasuredValue = new Measurement(12.6m, Measured),
        Observations = observations,
    };

    [Fact]
    public async Task Cuva_svaki_parametar_sa_svojom_jedinicom()
    {
        await SaveAsync(Reading(
            Observation(ObservationParameter.WaterTemperature, 22.0m, "°C"),
            Observation(ObservationParameter.Flow, 5.824m, "m³/s")));

        await using var context = postgres.CreateContext();
        var rows = await context.Observations.OrderBy(o => o.Parameter).ToListAsync();

        Assert.Equal(2, rows.Count);
        // Jedinica se čuva doslovno; preračunavanje bi bilo tiha prilika za faktor sto.
        Assert.Contains(rows, r => r.Unit == "°C" && r.Value == 22.0m);
        Assert.Contains(rows, r => r.Unit == "m³/s" && r.Value == 5.824m);
    }

    [Fact]
    public async Task Isti_podatak_dva_puta_daje_jedan_red()
    {
        // Izvor se osvježava na sat, a pitamo ga svakih 15 minuta. Bez idempotentnosti bi
        // isti podatak ušao četiri puta i graf bi imao stepenice kojih u rijeci nema.
        var observation = Observation(ObservationParameter.WaterTemperature, 22.0m, "°C");

        await SaveAsync(Reading(observation));
        await SaveAsync(Reading(observation));

        await using var context = postgres.CreateContext();
        Assert.Equal(1, await context.Observations.CountAsync());
    }

    [Fact]
    public async Task Dva_parametra_u_istom_satu_su_dva_reda()
    {
        // Da parametar nije u jedinstvenom ključu, temperatura i proticaj izmjereni u isti
        // sat bili bi isti red i jedan bi tiho pregazio drugi.
        await SaveAsync(Reading(
            Observation(ObservationParameter.WaterTemperature, 22.0m, "°C"),
            Observation(ObservationParameter.AirTemperature, 28.4m, "°C")));

        await using var context = postgres.CreateContext();
        Assert.Equal(2, await context.Observations.CountAsync());
    }

    [Fact]
    public async Task Historija_raste_kroz_vrijeme_a_ne_prepisuje_se()
    {
        await SaveAsync(Reading(Observation(ObservationParameter.WaterTemperature, 22.0m, "°C")));
        await SaveAsync(Reading(Observation(
            ObservationParameter.WaterTemperature, 22.4m, "°C", Measured.AddHours(1))));

        await using var context = postgres.CreateContext();
        var rows = await context.Observations.OrderBy(o => o.MeasuredAt).ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal([22.0m, 22.4m], rows.Select(r => r.Value));
    }

    [Fact]
    public async Task Vodostaj_ne_moze_uci_u_tabelu_mjerenja_parametara()
    {
        // Vodostaj živi u `measurements`. Isti broj u dvije tabele su dva mjesta koja se s
        // vremenom raziđu, a onda se ne zna koje je tačno. Shema to brani, ne samo kod.
        await using var context = postgres.CreateContext();

        context.Observations.Add(new ObservationRow
        {
            SourceId = "avp-sava-wiski",
            StationKey = "9019",
            Parameter = ObservationParameter.WaterLevel,
            ParameterLabelOriginal = "Vodostaj",
            Value = 12.6m,
            Unit = "cm",
            MeasuredAt = Measured,
            FirstFetchedAt = Fetched,
        });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        var postgresError = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal("ck_observations_no_water_level", postgresError.ConstraintName);
    }

    [Fact]
    public async Task Stanica_bez_vodostaja_i_dalje_cuva_svoja_mjerenja()
    {
        // 53 stanice mjere samo podzemnu vodu ili padavine. Da mjerenja vise o vodostaju,
        // svih 53 bi nestalo iz baze zajedno sa vodostajem kojeg nemaju.
        await SaveAsync(new StationReading.NoData
        {
            Station = Station(),
            StatusLabelOriginal = "",
            Reason = "Ova stanica ne mjeri vodostaj.",
            Observations = [Observation(ObservationParameter.GroundwaterLevel, 3.21m, "m")],
        });

        await using var context = postgres.CreateContext();

        Assert.Equal(1, await context.Observations.CountAsync());
        Assert.Equal(0, await context.Measurements.CountAsync());
    }
}
