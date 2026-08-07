using Vodostaji.Core;

namespace Vodostaji.Core.Tests;

/// <summary>
/// Izmjereni interval po stanici.
///
/// Adapter deklariše kadencu izvora jer drugo ne zna unaprijed. Ali kod FHMZBIH-a se većina
/// stanica javlja svaka dva sata, Reljevo svakih pet, a Bihać **jednom dnevno** — mjereno na
/// tri dana stvarnih podataka. Sa jednim intervalom za sve, Bihać trajno stoji kao zastario
/// iako radi po svom rasporedu, a signal koji je uvijek upaljen korisnik prestane gledati.
/// </summary>
public class ObservedIntervalsTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);

    private static StationReading Reading(string key, TimeSpan declared) =>
        new StationReading.Measured
        {
            Station = new Station
            {
                SourceId = "fhmzbih",
                StationKey = key,
                Name = key,
                ExpectedInterval = declared,
                Attribution = new Attribution
                {
                    AgencyName = "test",
                    AgencyUrl = new Uri("https://example.invalid"),
                },
            },
            StatusLabelOriginal = "",
            ClaimedLevel = AlertLevel.Unknown,
            MeasuredValue = new Measurement(100m, Now.AddHours(-3)),
        };

    private static SourceFetchResult Result(params StationReading[] readings) => new()
    {
        SourceId = "fhmzbih",
        FetchedAt = Now,
        Readings = readings,
    };

    [Fact]
    public void Izmjereni_interval_zamjenjuje_deklarisani()
    {
        var result = Result(Reading("Bihać", TimeSpan.FromHours(1)))
            .WithObservedIntervals(new Dictionary<string, TimeSpan>
            {
                ["Bihać"] = TimeSpan.FromHours(24),
            });

        Assert.Equal(TimeSpan.FromHours(24), result.Readings[0].Station.ExpectedInterval);
    }

    [Fact]
    public void Stanica_koja_se_javlja_dnevno_vise_ne_ispada_zastarjela()
    {
        var declared = Result(Reading("Bihać", TimeSpan.FromHours(1)));
        var measured = declared.WithObservedIntervals(new Dictionary<string, TimeSpan>
        {
            ["Bihać"] = TimeSpan.FromHours(24),
        });

        var before = declared.Readings[0].Station
            .MissedCycles(declared.Readings[0].Measurement!.MeasuredAt, Now);
        var after = measured.Readings[0].Station
            .MissedCycles(measured.Readings[0].Measurement!.MeasuredAt, Now);

        // UI.md §2: preko 3× interval je "zastario". Sa satom, tri sata su tri ciklusa.
        Assert.True(before >= 3, $"sa deklarisanim satom ispada zastarjelo ({before})");
        Assert.True(after < 1, $"sa izmjerenih 24h mora ispasti svježe ({after})");
    }

    [Fact]
    public void Stanica_bez_izmjerenog_intervala_zadrzava_deklarisani()
    {
        var result = Result(Reading("Nova", TimeSpan.FromHours(1)))
            .WithObservedIntervals(new Dictionary<string, TimeSpan> { ["Druga"] = TimeSpan.FromHours(6) });

        // Nova stanica nema historiju; deklaracija adaptera je jedino što imamo.
        Assert.Equal(TimeSpan.FromHours(1), result.Readings[0].Station.ExpectedInterval);
    }

    [Fact]
    public void Besmislen_izmjereni_interval_se_ignorise()
    {
        var result = Result(Reading("Bihać", TimeSpan.FromHours(1)))
            .WithObservedIntervals(new Dictionary<string, TimeSpan> { ["Bihać"] = TimeSpan.Zero });

        Assert.Equal(TimeSpan.FromHours(1), result.Readings[0].Station.ExpectedInterval);
    }

    [Fact]
    public void Zamjena_ne_dira_ni_vrijednost_ni_vrijeme_ni_stupanj()
    {
        var before = Assert.IsType<StationReading.Measured>(Reading("Bihać", TimeSpan.FromHours(1)));
        var after = Assert.IsType<StationReading.Measured>(
            Result(before)
                .WithObservedIntervals(new Dictionary<string, TimeSpan>
                {
                    ["Bihać"] = TimeSpan.FromHours(24),
                })
                .Readings[0]);

        // Mijenja se samo očekivanje o ritmu. Mjerenje ostaje netaknuto.
        Assert.Equal(before.MeasuredValue, after.MeasuredValue);
        Assert.Equal(before.ClaimedLevel, after.ClaimedLevel);
        Assert.Equal(before.Station.Name, after.Station.Name);
    }
}
