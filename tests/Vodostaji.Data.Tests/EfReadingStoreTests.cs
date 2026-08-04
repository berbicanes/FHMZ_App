using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vodostaji.Core;
using Vodostaji.Data;

namespace Vodostaji.Data.Tests;

[Collection(PostgresCollection.Name)]
public class EfReadingStoreTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Fetched = new(2026, 8, 4, 23, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Measured = new(2026, 8, 4, 21, 0, 0, TimeSpan.Zero);

    private static readonly Attribution Sava = new()
    {
        AgencyName = "Agencija za vodno područje rijeke Save",
        AgencyUrl = new Uri("https://www.voda.ba"),
    };

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Station Station(string key, Coordinates? coordinates = null) => new()
    {
        SourceId = "avp-sava",
        StationKey = key,
        Name = $"Bosna-{key}",
        River = "Bosna",
        Coordinates = coordinates,
        ExpectedInterval = TimeSpan.FromHours(1),
        Attribution = Sava,
    };

    private async Task<int> SaveAsync(params StationReading[] readings)
    {
        await using var context = postgres.CreateContext();
        var store = new EfReadingStore(context);

        await store.SaveAsync(
            new SourceFetchResult
            {
                SourceId = "avp-sava",
                FetchedAt = Fetched,
                Readings = readings,
            },
            CancellationToken.None);

        return readings.Length;
    }

    [Fact]
    public async Task Mjerenje_prezivi_zapis_bez_gubitka_preciznosti()
    {
        await SaveAsync(new StationReading.Measured
        {
            Station = Station("1"),
            StatusLabelOriginal = "Standby",
            ClaimedLevel = AlertLevel.Normal,
            MeasuredValue = new Measurement(17.7000008m, Measured),
        });

        await using var context = postgres.CreateContext();
        var state = await context.StationStates.SingleAsync();

        // `numeric` bez zadate preciznosti. Artefakt jednostruke preciznosti iz izvora
        // stiže na disk netaknut — zaokruživanje bi bilo tiho mijenjanje tuđeg mjerenja.
        Assert.Equal(17.7000008m, state.ValueCm);
        Assert.Equal(Measured, state.MeasuredAt);
        Assert.Equal(Fetched, state.FetchedAt);
        Assert.NotEqual(state.MeasuredAt, state.FetchedAt);
    }

    [Fact]
    public async Task Zapis_bez_podatka_ide_u_bazu_kao_unknown_sa_razlogom()
    {
        await SaveAsync(new StationReading.NoData
        {
            Station = Station("2"),
            StatusLabelOriginal = "No Data",
            Reason = "`DATE_TIME` je null.",
        });

        await using var context = postgres.CreateContext();
        var state = await context.StationStates.SingleAsync();

        Assert.Equal(AlertLevel.Unknown, state.Level);
        Assert.Null(state.ValueCm);
        Assert.Null(state.MeasuredAt);
        Assert.Equal("No Data", state.StatusLabelOriginal);
        Assert.Contains("DATE_TIME", state.NoDataReason!, StringComparison.Ordinal);

        // Bez podatka nema ni reda u historiji — historija je niz mjerenja, ne niz pokušaja.
        Assert.Equal(0, await context.Measurements.CountAsync());
    }

    [Fact]
    public async Task Ponovljeni_upis_istog_mjerenja_ne_duplira_historiju()
    {
        StationReading reading = new StationReading.Measured
        {
            Station = Station("3"),
            StatusLabelOriginal = "Standby",
            ClaimedLevel = AlertLevel.Normal,
            MeasuredValue = new Measurement(120m, Measured),
        };

        // Izvor se osvježava na sat, a mi pitamo svakih 15 minuta — isti podatak stiže
        // četiri puta i smije ući samo jednom.
        await SaveAsync(reading);
        await SaveAsync(reading);
        await SaveAsync(reading);
        await SaveAsync(reading);

        await using var context = postgres.CreateContext();
        Assert.Equal(1, await context.Measurements.CountAsync());
        Assert.Equal(1, await context.StationStates.CountAsync());
    }

    [Fact]
    public async Task Novo_mjerenje_dodaje_red_a_ne_mijenja_stari()
    {
        await SaveAsync(new StationReading.Measured
        {
            Station = Station("4"),
            StatusLabelOriginal = "Standby",
            ClaimedLevel = AlertLevel.Normal,
            MeasuredValue = new Measurement(120m, Measured),
        });

        await SaveAsync(new StationReading.Measured
        {
            Station = Station("4"),
            StatusLabelOriginal = "Regular defence",
            ClaimedLevel = AlertLevel.Elevated,
            MeasuredValue = new Measurement(160m, Measured.AddHours(1)),
        });

        await using var context = postgres.CreateContext();

        var history = await context.Measurements.OrderBy(m => m.MeasuredAt).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(120m, history[0].ValueCm);
        Assert.Equal(AlertLevel.Normal, history[0].Level);
        Assert.Equal(160m, history[1].ValueCm);

        var state = await context.StationStates.SingleAsync();
        Assert.Equal(160m, state.ValueCm);
        Assert.Equal(AlertLevel.Elevated, state.Level);
    }

    [Fact]
    public async Task Stanica_koja_izostane_iz_odgovora_se_ne_brise()
    {
        await SaveAsync(
            NormalAt("5", 100m),
            NormalAt("6", 200m));

        // Sljedeći odgovor donosi samo jednu stanicu. Nestanak iz jednog odgovora nije
        // dokaz da je stanica prestala postojati (zlatno pravilo 5).
        await SaveAsync(NormalAt("5", 110m));

        await using var context = postgres.CreateContext();
        Assert.Equal(2, await context.StationStates.CountAsync());
        Assert.Equal(2, await context.Stations.CountAsync());

        var untouched = await context.StationStates.SingleAsync(s => s.StationKey == "6");
        Assert.Equal(200m, untouched.ValueCm);
    }

    [Fact]
    public async Task Koordinate_se_ne_brisu_kad_ih_odgovor_ne_donese()
    {
        await SaveAsync(new StationReading.Measured
        {
            Station = Station("7", new Coordinates(44.2, 17.9)),
            StatusLabelOriginal = "Standby",
            ClaimedLevel = AlertLevel.Normal,
            MeasuredValue = new Measurement(100m, Measured),
        });

        await SaveAsync(NormalAt("7", 105m, Measured.AddHours(1)));

        await using var context = postgres.CreateContext();
        var station = await context.Stations.SingleAsync();

        Assert.Equal(44.2, station.Latitude);
        Assert.Equal(17.9, station.Longitude);
    }

    [Fact]
    public async Task Baza_odbija_nepoznato_koje_se_predstavlja_kao_normalno()
    {
        await using var context = postgres.CreateContext();

        // Zaobilazi domenske tipove namjerno — pitanje je da li shema drži i kad kod ne drži.
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO station_states
                  ("SourceId","StationKey","FetchedAt","Level","StatusLabelOriginal")
                VALUES ('avp-sava','ручно', now(), 'Normal', 'Standby');
                """));

        Assert.Contains("unknown_never_normal", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Baza_odbija_vrijednost_bez_vremena_mjerenja()
    {
        await using var context = postgres.CreateContext();

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO station_states
                  ("SourceId","StationKey","FetchedAt","Level","StatusLabelOriginal","ValueCm")
                VALUES ('avp-sava','bez-vremena', now(), 'Normal', 'Standby', 42);
                """));

        Assert.Contains("value_needs_time", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Skladiste_broji_stanice_po_izvoru()
    {
        await SaveAsync(NormalAt("8", 100m), NormalAt("9", 100m));

        await using var context = postgres.CreateContext();
        var store = new EfReadingStore(context);

        Assert.Equal(2, await store.CountAsync("avp-sava", CancellationToken.None));
        Assert.Equal(0, await store.CountAsync("avpjm", CancellationToken.None));
    }

    private static StationReading NormalAt(string key, decimal value, DateTimeOffset? at = null) =>
        new StationReading.Measured
        {
            Station = Station(key),
            StatusLabelOriginal = "Standby",
            ClaimedLevel = AlertLevel.Normal,
            MeasuredValue = new Measurement(value, at ?? Measured),
        };
}
