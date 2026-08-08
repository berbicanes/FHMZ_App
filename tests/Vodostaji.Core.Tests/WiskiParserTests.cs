using Vodostaji.Core;
using Vodostaji.Ingest;
using Vodostaji.Ingest.Wiski;

namespace Vodostaji.Core.Tests;

/// <summary>
/// WISKI izvoz AVP Save — četvrti izvor, i prvi sa više od jednog parametra po stanici.
///
/// Težište testova nije na čitanju JSON-a, nego na tome **šta se odbacuje i šta se ne
/// izvodi**: red bez vremena, koordinata izvan zemlje, klasa iz koje bi se dao izmisliti
/// stupanj opasnosti.
/// </summary>
public class WiskiParserTests
{
    private const string Date = "2026-08-08";

    private static readonly Attribution Attribution = new()
    {
        AgencyName = "Agencija za vodno područje rijeke Save",
        AgencyUrl = new Uri("https://www.voda.ba"),
    };

    private static readonly SourceClock Clock = new()
    {
        Convention = ClockConvention.ExplicitInValue,
        Evidence = "Pomak je u vrijednosti; vidi SOURCES.md §4.5.",
    };

    private static ParsedWiskiLayer Layer(int id) =>
        WiskiParser.ParseLayer(Fixture.Read($"avp-sava-wiski/layer-{id}-{Date}.json"), Clock);

    private static IReadOnlyList<StationReading> MergeAll(params int[] layers) =>
        WiskiSource.Merge(layers.SelectMany(id => Layer(id).Rows).ToList(), Attribution);

    [Fact]
    public void Cita_sve_parametre_sa_njihovim_jedinicama()
    {
        var expected = new (int Layer, ObservationParameter Parameter, string Unit)[]
        {
            (10, ObservationParameter.WaterLevel, "cm"),
            (20, ObservationParameter.Flow, "m³/s"),
            (30, ObservationParameter.WaterTemperature, "°C"),
            (40, ObservationParameter.GroundwaterLevel, "m"),
            (50, ObservationParameter.GroundwaterTemperature, "°C"),
            (60, ObservationParameter.Precipitation, "mm"),
            (70, ObservationParameter.AirTemperature, "°C"),
        };

        foreach (var (id, parameter, unit) in expected)
        {
            var rows = Layer(id).Rows;

            Assert.NotEmpty(rows);
            Assert.All(rows, r => Assert.Equal(parameter, r.Parameter));
            // Jedinica se prepisuje doslovno i nikad ne preračunava.
            Assert.All(rows, r => Assert.Equal(unit, r.Unit));
        }
    }

    [Fact]
    public void Vrijeme_se_cita_iz_vrijednosti_a_ne_rekonstruise()
    {
        // Ovo je jedina konvencija bez rizika: pomak stoji u samoj vrijednosti.
        // `2026-08-08T21:00:00.000+02:00` je 19:00Z, i to bez ijedne pretpostavke o zoni.
        var row = Layer(30).Rows.First();

        Assert.Equal(TimeSpan.Zero, row.MeasuredAt.Offset);
        Assert.Equal(0, row.MeasuredAt.Minute);
    }

    [Fact]
    public void Red_bez_vremena_ili_vrijednosti_se_preskace_sa_razlogom()
    {
        // Sloj 80 je zato i izostavljen iz izvora: većina redova nema `L1_timestamp`.
        var parsed = Layer(80);

        Assert.NotEmpty(parsed.Skipped);
        Assert.All(parsed.Skipped, s => Assert.False(string.IsNullOrWhiteSpace(s.Reason)));
        // Preskočeni su preskočeni, ne tiho pretvoreni u nulu.
        Assert.True(parsed.Skipped.Count > parsed.Rows.Count);
    }

    [Fact]
    public void Nijedna_stanica_ne_dobija_stupanj_opasnosti()
    {
        // Izvoz ne objavljuje nijedan prag ni ocjenu. `#TH1#` liči na klasu praga, ali
        // legenda nije objavljena — pretvoriti je u stupanj bilo bi pogađanje (pravilo 3).
        Assert.All(MergeAll(10, 20, 30), r => Assert.Equal(AlertLevel.Unknown, r.Level));
    }

    [Fact]
    public void Klasa_iz_izvora_se_cuva_doslovno()
    {
        var withClass = MergeAll(10)
            .OfType<StationReading.Measured>()
            .Where(r => r.StatusLabelOriginal.Length > 0)
            .ToList();

        Assert.NotEmpty(withClass);
        Assert.All(withClass, r => Assert.StartsWith("#", r.StatusLabelOriginal));
    }

    [Fact]
    public void Stanica_sa_vodostajem_nosi_ostala_mjerenja_uz_njega()
    {
        var measured = MergeAll(10, 20, 30)
            .OfType<StationReading.Measured>()
            .First(r => r.Observations.Count > 0);

        // Vodostaj je mjerenje stanice; ostalo su zapažanja. Isti broj se ne ponavlja.
        Assert.DoesNotContain(
            measured.Observations,
            o => o.Parameter == ObservationParameter.WaterLevel);

        Assert.All(measured.Observations, o => Assert.False(string.IsNullOrWhiteSpace(o.Unit)));
    }

    [Fact]
    public void Stanica_koja_ne_mjeri_vodostaj_nije_stanica_bez_podatka()
    {
        // Ovo je razlika koju zlatno pravilo 1 traži u drugom smjeru: imamo podatak, samo
        // nije vodostaj. Razlog mora reći šta stanica stvarno mjeri.
        var readings = MergeAll(30, 60, 70);

        var noLevel = readings.OfType<StationReading.NoData>()
            .Where(r => r.Observations.Count > 0)
            .ToList();

        Assert.NotEmpty(noLevel);
        Assert.All(noLevel, r => Assert.Contains("ne mjeri vodostaj", r.Reason));
        Assert.All(noLevel, r => Assert.Equal(AlertLevel.Unknown, r.Level));
    }

    [Fact]
    public void Svako_mjerenje_nosi_vlastito_vrijeme()
    {
        // Ista stanica ima svjež vodostaj i podzemnu vodu staru mjesecima. Jedan timestamp
        // po stanici bi jedno od to dvoje pretvorio u laž.
        var mixed = MergeAll(10, 40, 50)
            .OfType<StationReading.Measured>()
            .FirstOrDefault(r => r.Observations.Any(
                o => o.Parameter == ObservationParameter.GroundwaterLevel));

        if (mixed is null) return;   // ne poklapaju se sve stanice; test nema šta tvrditi

        var groundwater = mixed.Observations.First(
            o => o.Parameter == ObservationParameter.GroundwaterLevel);

        Assert.NotEqual(mixed.MeasuredValue.MeasuredAt, groundwater.MeasuredAt);
    }

    [Fact]
    public void Ime_rijeke_dolazi_iz_izvora_a_ne_iz_naziva_stanice()
    {
        // Stari adapter je rijeku vadio cijepanjem `Rijeka-Mjesto` na crtici. Ovdje je to
        // vlastito polje, pa Vrhpolje ne može završiti na Uni (SOURCES.md §3.1).
        var named = MergeAll(10).Where(r => r.Station.River is not null).ToList();

        Assert.NotEmpty(named);
        Assert.All(named, r => Assert.DoesNotContain("-", r.Station.River!));
    }

    [Fact]
    public void Koordinate_izvan_bih_se_odbacuju()
    {
        // Stanica u pogrešnoj zemlji je gora od stanice bez koordinata: prva se nacrta.
        const string json = """
            [{"metadata_station_no":"1","metadata_station_name":"Test",
              "metadata_station_latitude":"0","metadata_station_longitude":"0",
              "L1_timestamp":"2026-08-08T21:00:00.000+02:00","L1_ts_value":"10",
              "L1_stationparameter_no":"H","L1_ts_unitsymbol":"cm"}]
            """;

        Assert.Null(WiskiParser.ParseLayer(json, Clock).Rows.Single().Coordinates);
    }

    [Fact]
    public void Nepoznat_parametar_se_prikazuje_a_ne_odbacuje()
    {
        // Kad izvor doda nešto novo, vrijednost postoji i mora se vidjeti — samo se ne
        // pretvara ni u šta naše.
        const string json = """
            [{"metadata_station_no":"1","metadata_station_name":"Test",
              "L1_timestamp":"2026-08-08T21:00:00.000+02:00","L1_ts_value":"7.5",
              "L1_stationparameter_no":"XYZ","L1_stationparameter_name":"Nešto novo",
              "L1_ts_unitsymbol":"kg"}]
            """;

        var row = WiskiParser.ParseLayer(json, Clock).Rows.Single();

        Assert.Equal(ObservationParameter.Unknown, row.Parameter);
        Assert.Equal("Nešto novo", row.ParameterLabel);
        Assert.Equal(7.5m, row.Value);
    }

    [Fact]
    public void Odgovor_koji_nije_lista_se_ne_cita_kao_prazan_izvor()
    {
        // Prazan rezultat bi ingest prihvatio kao uspješan i ostavio sve bez podatka.
        Assert.Throws<SourceResponseException>(
            () => WiskiParser.ParseLayer("""{"error":"nope"}""", Clock));
    }
}
