using Vodostaji.Core;
using Vodostaji.Ingest;
using Vodostaji.Ingest.Avpjm;

namespace Vodostaji.Core.Tests;

/// <summary>
/// AVPJM — drugi izvor, i prva provjera da li model podnosi nejednakost.
///
/// AVP Sava mjeri na sat i objavljuje stupanj opasnosti; AVPJM mjeri svakih 15 minuta i
/// stupanj **ne objavljuje**. Da model to ne podnosi, pucalo bi ovdje.
/// </summary>
public class AvpjmListParserTests
{
    private const string ListFixture = "avpjm/vodomjerne-stanice-lista-2026-08-04.html";

    private static readonly Attribution Attribution = new()
    {
        AgencyName = "Agencija za vodno područje Jadranskog mora",
        AgencyUrl = new Uri("https://avpjm.jadran.ba"),
    };

    private static readonly SourceClock Clock = new()
    {
        Convention = ClockConvention.FixedOffset,
        FixedOffset = TimeSpan.FromHours(1),
        Evidence = "Polje `owner` kaže 'zimsko računanje vremena'; vidi SOURCES.md §2.",
    };

    private static ParsedAvpjm ParseList() =>
        AvpjmListParser.Parse(
            Fixture.Read(ListFixture), Clock, Attribution,
            TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30));

    [Fact]
    public void Lista_nosi_cijeli_sliv_u_jednom_zahtjevu()
    {
        var parsed = ParseList();

        // U Fazi 0 je pisalo da lista nema podatke. Bilo je pogrešno — podaci su
        // HTML-escapovan JSON u Vue propu. Ovaj test čuva ispravku.
        Assert.Equal(20, parsed.Readings.Count);
        Assert.Empty(parsed.Skipped);
    }

    [Fact]
    public void Nijedna_stanica_ne_dobija_stupanj_opasnosti()
    {
        var parsed = ParseList();

        // AVPJM ne objavljuje stupanj javnosti — bojenje im je zaključano na internu ulogu
        // `fop` (SOURCES.md §2.1). Izvođenje bi značilo pokazati ocjenu koju agencija
        // namjerno ne pokazuje, po pravilu koje ni sami ne razumijemo.
        Assert.All(parsed.Readings, r => Assert.Equal(AlertLevel.Unknown, r.Level));
    }

    [Fact]
    public void Vrijednost_je_ipak_tu_jer_nepoznat_stupanj_nije_nepoznat_podatak()
    {
        var parsed = ParseList();

        var measured = parsed.Readings.OfType<StationReading.Measured>().ToList();

        // Ovo je slučaj zbog kojeg model razlikuje "nemamo broj" od "nemamo tvrdnju o broju".
        Assert.Equal(20, measured.Count);
        Assert.All(measured, r => Assert.NotNull(r.Measurement));
    }

    [Fact]
    public void Mostar_se_cita_tacno_kako_je_snimljen()
    {
        var mostar = Assert.IsType<StationReading.Measured>(
            ParseList().Readings.Single(r => r.Station.Name == "Mostar"));

        Assert.Equal("1", mostar.Station.StationKey);
        Assert.Equal("Neretva", mostar.Station.River);
        Assert.Equal(244m, mostar.MeasuredValue.ValueCm);
    }

    [Fact]
    public void Vrijeme_se_cita_kao_zimsko_i_ljeti()
    {
        var mostar = Assert.IsType<StationReading.Measured>(
            ParseList().Readings.Single(r => r.Station.Name == "Mostar"));

        // `valtime` 1785885300 naivno je 2026-08-04 23:15Z. Kao CET je 22:15Z.
        // Naivno čitanje bi podatak stavilo 42 minute u budućnost.
        Assert.Equal(
            new DateTimeOffset(2026, 8, 4, 22, 15, 0, TimeSpan.Zero),
            mostar.MeasuredValue.MeasuredAt);
    }

    [Fact]
    public void Koordinate_se_citaju_kao_lat_lon_a_ne_obrnuto()
    {
        var mostar = ParseList().Readings.Single(r => r.Station.Name == "Mostar");
        var coordinates = Assert.IsType<Coordinates>(mostar.Station.Coordinates);

        // `location` je "43.34835,17.8105" — lat pa lon, obrnuto od GeoJSON-a.
        // Zamjena bi Mostar prebacila u Irak.
        Assert.Equal(43.34835, coordinates.Latitude, 5);
        Assert.Equal(17.8105, coordinates.Longitude, 4);
    }

    [Fact]
    public void Kota_sa_punom_double_ekspanzijom_se_svodi_na_namjeravanu_vrijednost()
    {
        var mostar = ParseList().Readings.Single(r => r.Station.Name == "Mostar");

        // Izvor šalje 40.28999999999999914734871708787977695465087890625 — 47 decimala.
        // To je artefakt zapisa doublea, ne preciznost mjerenja.
        Assert.Equal(40.29m, mostar.Station.GaugeZero);
    }

    [Fact]
    public void Vecina_stanica_nema_nijedan_prag_i_to_je_normalno_stanje()
    {
        var parsed = ParseList();

        var withThresholds = parsed.Readings.Count(r => r.Thresholds is { IsEmpty: false });

        // 6 od 20 ima bar jedan prag; samo 3 imaju par redovna+vanredna. Ostalih 14 nema
        // nijedan. Prazan prag je nepoznat prag, ne nula.
        Assert.Equal(6, withThresholds);

        var withPair = parsed.Readings.Count(r =>
            r.Thresholds?.Values.Count(t => t.LabelOriginal is "redovna_obrana" or "vanredna_obrana") == 2);
        Assert.Equal(3, withPair);
    }

    [Fact]
    public void Pragovi_zadrzavaju_imena_izvora_i_nemaju_dodijeljen_stupanj()
    {
        var capljina = ParseList().Readings.Single(r => r.Station.Name == "Čapljina");
        var thresholds = Assert.IsType<Thresholds>(capljina.Thresholds);

        Assert.Equal(
            ["redovna_obrana", "vanredna_obrana"],
            thresholds.Values.Select(t => t.LabelOriginal).ToArray());
        Assert.Equal([200m, 250m], thresholds.Values.Select(t => t.ValueCm).ToArray());

        // Redoslijed pragova kod AVPJM-a nije skala ozbiljnosti, pa se stupanj ne dodjeljuje.
        Assert.All(thresholds.Values, t => Assert.Null(t.Level));
    }

    [Fact]
    public void Negativan_vodostaj_je_legitiman()
    {
        var capljina = Assert.IsType<StationReading.Measured>(
            ParseList().Readings.Single(r => r.Station.Name == "Čapljina"));

        // Kota nule letve nije dno rijeke.
        Assert.Equal(-261m, capljina.MeasuredValue.ValueCm);
    }

    [Fact]
    public void Stranica_bez_ocekivanog_propa_se_ne_cita_kao_prazan_sliv()
    {
        // Ako promijene stranicu, prazan rezultat bi izgledao kao "nema nijedne stanice",
        // a to bi ingest job prihvatio kao uspješan i posivio cijeli jug.
        Assert.Throws<SourceResponseException>(() =>
            AvpjmListParser.Parse(
                "<html><body><p>Održavanje</p></body></html>",
                Clock, Attribution, TimeSpan.FromMinutes(15)));
    }
}
