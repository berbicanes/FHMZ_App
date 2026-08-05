using Vodostaji.Core;
using Vodostaji.Ingest;
using Vodostaji.Ingest.Fhmzbih;

namespace Vodostaji.Core.Tests;

/// <summary>
/// FHMZBIH — treći izvor, treća konvencija.
///
/// AVP Sava daje stupanj opasnosti i mjeri na sat. AVPJM ne daje stupanj i drži zimsko
/// vrijeme cijele godine. FHMZBIH ne daje stupanj, **poštuje ljetno vrijeme**, i jedini od
/// njih **objavljuje trend**.
/// </summary>
public class FhmzbihParserTests
{
    private const string IndexFixture = "fhmzbih/hidro-index-2026-08-05.html";
    private const string ZenicaFixture = "fhmzbih/hvs-zenica-2026-08-05.html";
    private const string BihacFixture = "fhmzbih/hvs-bihac-2026-08-05.html";

    private static readonly Attribution Attribution = new()
    {
        AgencyName = "Federalni hidrometeorološki zavod BiH",
        AgencyUrl = new Uri("https://www.fhmzbih.gov.ba"),
    };

    private static readonly SourceClock Clock = new()
    {
        Convention = ClockConvention.LocalWithDst,
        TimeZoneId = "Europe/Sarajevo",
        Evidence = "Dokazano 2026-08-04; vidi SOURCES.md §3.",
    };

    private static ParsedFhmzbih ParseIndex(
        IReadOnlyDictionary<string, FhmzbihStationDetails>? details = null) =>
        FhmzbihParser.ParseIndex(
            Fixture.Read(IndexFixture), Clock, Attribution,
            TimeSpan.FromHours(1), TimeSpan.FromHours(1), details);

    [Fact]
    public void Cita_svih_dvanaest_stanica()
    {
        var parsed = ParseIndex();

        Assert.Equal(12, parsed.Readings.Count);
        Assert.Empty(parsed.Skipped);
    }

    [Fact]
    public void Vodotok_se_nasljeduje_kroz_spojene_celije()
    {
        var parsed = ParseIndex();

        // `Una` nosi `rowspan`, pa ga red Martin Broda nema u vlastitoj ćeliji.
        var martinBrod = parsed.Readings.Single(r => r.Station.Name == "Martin Brod");
        Assert.Equal("Una", martinBrod.Station.River);
    }

    [Fact]
    public void Nijedna_stanica_ne_dobija_stupanj_opasnosti()
    {
        var parsed = ParseIndex();

        // Legenda sa tri stanja postoji na stranici, ali nijedan red tabele ne nosi njenu
        // ikonu, a ni podstranice stanica. Imamo broj, nemamo tvrdnju o njemu.
        Assert.All(parsed.Readings, r => Assert.Equal(AlertLevel.Unknown, r.Level));
    }

    [Fact]
    public void Trend_se_cita_iz_izvora_a_ne_izvodi()
    {
        var parsed = ParseIndex();

        var withTrend = parsed.Readings
            .OfType<StationReading.Measured>()
            .Where(r => r.Trend is not null)
            .ToList();

        Assert.NotEmpty(withTrend);

        // Oznaka agencije se čuva doslovno uz naše mapiranje.
        Assert.All(withTrend, r => Assert.False(string.IsNullOrWhiteSpace(r.Trend!.LabelOriginal)));
    }

    [Fact]
    public void Nepoznata_oznaka_trenda_se_prijavljuje_a_ne_pogadja()
    {
        var parsed = ParseIndex();

        // `S2` se pojavljuje u podacima a značenje mu nije dokumentovano. Mora ostati
        // `Unknown` — pogrešno pogođen smjer trenda je pogrešan podatak o rijeci.
        foreach (var reading in parsed.Readings.OfType<StationReading.Measured>())
        {
            if (reading.Trend is { Direction: TrendDirection.Unknown } trend)
            {
                Assert.Contains(trend.LabelOriginal, parsed.UnrecognisedTrends);
            }
        }

        Assert.All(
            parsed.UnrecognisedTrends,
            code => Assert.DoesNotContain(code, new[] { "R", "O", "S" }));
    }

    [Fact]
    public void Vrijeme_se_cita_kao_lokalno_sa_ljetnim_pomakom()
    {
        var parsed = ParseIndex();

        var bihac = Assert.IsType<StationReading.Measured>(
            parsed.Readings.Single(r => r.Station.Name == "Bihać"));

        // Stranica piše `5.8.2026` i `08:00` lokalno. Ljeti je to CEST, dakle 06:00Z.
        Assert.Equal(
            new DateTimeOffset(2026, 8, 5, 6, 0, 0, TimeSpan.Zero),
            bihac.MeasuredValue.MeasuredAt);
    }

    [Fact]
    public void Prag_nosi_ime_iz_zaglavlja_i_nema_dodijeljen_stupanj()
    {
        var parsed = ParseIndex();

        var bihac = parsed.Readings.Single(r => r.Station.Name == "Bihać");
        var thresholds = Assert.IsType<Thresholds>(bihac.Thresholds);

        Assert.Equal(
            "Kontinuirano obavještavanje stanovništva i CZ",
            thresholds.Values[0].LabelOriginal);

        // Agencija ne kaže da prelazak tog praga znači konkretan stepen opasnosti.
        Assert.All(thresholds.Values, t => Assert.Null(t.Level));
    }

    [Fact]
    public void Podstranica_daje_koordinate_i_kotu_nule()
    {
        var zenica = FhmzbihParser.ParseStationPage(Fixture.Read(ZenicaFixture), "Zenica");

        Assert.NotNull(zenica);
        Assert.Equal("Bosna", zenica!.River);
        Assert.Equal("Sava", zenica.Basin);

        var coordinates = Assert.IsType<Coordinates>(zenica.Coordinates);
        Assert.Equal(44.20795, coordinates.Latitude, 5);
        Assert.Equal(17.90702, coordinates.Longitude, 5);

        // Ključ za kotu nule nije stabilan — na Zenici je `(m n.m.9`, na Bihaću `(m n.m.)`.
        Assert.Equal(307.600m, zenica.GaugeZero);
    }

    [Fact]
    public void Nestabilan_naziv_kljuca_za_kotu_ne_lomi_citanje()
    {
        var bihac = FhmzbihParser.ParseStationPage(Fixture.Read(BihacFixture), "Bihać");

        Assert.NotNull(bihac);
        Assert.Equal(219.830m, bihac!.GaugeZero);
        Assert.Equal("Una", bihac.River);
    }

    [Fact]
    public void Detalji_sa_podstranica_dopunjuju_stanice_iz_pregleda()
    {
        var details = new Dictionary<string, FhmzbihStationDetails>(StringComparer.OrdinalIgnoreCase)
        {
            ["Zenica"] = FhmzbihParser.ParseStationPage(Fixture.Read(ZenicaFixture), "Zenica")!,
        };

        var zenica = ParseIndex(details).Readings.Single(r => r.Station.Name == "Zenica");

        // Bez koordinata stanica ne može na mapu; pregled ih ne daje, podstranica daje.
        Assert.NotNull(zenica.Station.Coordinates);
        Assert.Equal(307.600m, zenica.Station.GaugeZero);
    }

    [Fact]
    public void Stranica_bez_tabele_se_ne_cita_kao_prazan_izvor()
    {
        // Prazan rezultat bi ingest job prihvatio kao uspješan i ostavio sve bez podatka.
        Assert.Throws<SourceResponseException>(() =>
            FhmzbihParser.ParseIndex(
                "<html><body><p>Održavanje</p></body></html>",
                Clock, Attribution, TimeSpan.FromHours(1), TimeSpan.FromHours(1)));
    }
}
