using System.Globalization;
using System.Text.Json;
using Vodostaji.Core;
using Vodostaji.Ingest.AvpSava;

namespace Vodostaji.Core.Tests;

/// <summary>
/// Sve tvrdnje ovdje su o snimku od 2026-08-04, ne o izmišljenom ulazu.
/// Kad izvor promijeni shemu, rječnik ili konvenciju, ovi testovi padaju.
/// </summary>
public class AvpSavaReachParserTests
{
    private const string SampleFixture =
        "avp-sava/Hidrolosko_stane_u_realnom_vremenu-FeatureServer-0-sample-outfields-all-2026-08-04.json";

    private static readonly Attribution Attribution = new()
    {
        AgencyName = "Agencija za vodno područje rijeke Save",
        AgencyUrl = new Uri("https://www.voda.ba"),
    };

    private static readonly SourceClock Clock = new()
    {
        Convention = ClockConvention.Utc,
        Evidence = "Dokazano 2026-08-04 upitom sa SQL literalom; vidi SOURCES.md §1.1.",
    };

    private static ParsedReaches ParseSample() =>
        AvpSavaReachParser.Parse(
            Fixture.Read(SampleFixture), Clock, Attribution, TimeSpan.FromHours(1));

    [Fact]
    public void Cita_sve_dionice_iz_snimka_i_ne_preskace_nijednu()
    {
        var parsed = ParseSample();

        Assert.Equal(25, parsed.Readings.Count);
        Assert.Empty(parsed.Skipped);
    }

    [Fact]
    public void Rjecnik_statusa_se_nije_promijenio()
    {
        var parsed = ParseSample();

        // Prazno je očekivano stanje. Ako ovdje išta osvane, izvor je uveo novi status
        // i AvpSavaStatusMap se mora dopuniti prije nego se ti zapisi smiju koristiti.
        Assert.Empty(parsed.UnrecognisedStatuses);
    }

    [Fact]
    public void Sedam_dionica_bez_podatka_ostaje_bez_podatka()
    {
        var parsed = ParseSample();

        var noData = parsed.Readings.OfType<StationReading.NoData>().ToList();

        // U uzorku od 25 zapisa snimljenom 2026-08-04: 18 `Standby`, 7 `No Data`.
        Assert.Equal(7, noData.Count);
        Assert.All(noData, r => Assert.Equal(AlertLevel.Unknown, r.Level));
        Assert.All(noData, r => Assert.Null(r.Measurement));
    }

    [Fact]
    public void Nijedna_dionica_bez_podatka_nije_postala_normalna()
    {
        var parsed = ParseSample();

        foreach (var reading in parsed.Readings.Where(r => r.Measurement is null))
        {
            Assert.Equal(AlertLevel.Unknown, reading.Level);
            Assert.NotEqual(AlertLevel.Normal, reading.Level);
        }
    }

    [Fact]
    public void Bosna_zenica_se_cita_tacno_kako_je_snimljena()
    {
        var parsed = ParseSample();

        var zenica = Assert.IsType<StationReading.Measured>(
            parsed.Readings.Single(r => r.Station.Name == "Bosna-Zenica"));

        Assert.Equal("Standby", zenica.StatusLabelOriginal);
        Assert.Equal(AlertLevel.Normal, zenica.ClaimedLevel);
        Assert.Equal("1", zenica.Station.StationKey);
        Assert.Equal("Bosna", zenica.Station.River);
    }

    /// <summary>
    /// Očekivanje se izvodi iz samog fixture-a, ne iz konstante prepisane u trenutku pisanja.
    /// Sonda prepisuje fixture istog dana kad se ponovo pokrene, pa bi ukucana vrijednost
    /// pravila lažne padove — a ono što se ovdje testira nije koliko je voda visoka nego da
    /// se broj prenosi bez gubitka preciznosti.
    /// </summary>
    [Fact]
    public void H_cm_se_prenosi_bez_gubitka_preciznosti()
    {
        var parsed = ParseSample();
        var zenica = Assert.IsType<StationReading.Measured>(
            parsed.Readings.Single(r => r.Station.Name == "Bosna-Zenica"));

        var raw = RawProperty("Bosna-Zenica", "H_CM");

        // H_CM je esriFieldTypeSingle i stiže sa artefaktom jednostruke preciznosti
        // (`17.7000008`). Artefakt se prenosi doslovno — bez zaokruživanja i bez prolaska
        // kroz double, koji bi ga promijenio.
        Assert.Equal(decimal.Parse(raw, CultureInfo.InvariantCulture), zenica.MeasuredValue.ValueCm);
        Assert.Contains(".", raw, StringComparison.Ordinal);
        Assert.Equal(raw, zenica.MeasuredValue.ValueCm.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Epoch_se_cita_kao_utc_jer_konverziju_radi_njihov_servis()
    {
        var parsed = ParseSample();
        var zenica = Assert.IsType<StationReading.Measured>(
            parsed.Readings.Single(r => r.Station.Name == "Bosna-Zenica"));

        var epoch = long.Parse(RawProperty("Bosna-Zenica", "DATE_TIME"), CultureInfo.InvariantCulture);

        // Baza drži lokalno zidno vrijeme, ali servis ga konvertuje prije slanja — dokazano
        // upitom u SOURCES.md §1.1. Epoch je dakle već UTC i ne pomjera se ni za sekundu.
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(epoch), zenica.MeasuredValue.MeasuredAt);
        Assert.Equal(TimeSpan.Zero, zenica.MeasuredValue.MeasuredAt.Offset);
    }

    private static string RawProperty(string description, string property)
    {
        using var document = JsonDocument.Parse(Fixture.Read(SampleFixture));

        return document.RootElement
            .GetProperty("features")
            .EnumerateArray()
            .Select(f => f.GetProperty("properties"))
            .Single(p => p.GetProperty("description").GetString() == description)
            .GetProperty(property)
            .GetRawText();
    }

    [Fact]
    public void Pragovi_se_cuvaju_sa_imenima_iz_izvora_i_imenom_agencije()
    {
        var parsed = ParseSample();

        var zenica = parsed.Readings.Single(r => r.Station.Name == "Bosna-Zenica");
        var thresholds = Assert.IsType<Thresholds>(zenica.Thresholds);

        Assert.Equal("Agencija za vodno područje rijeke Save", thresholds.DefinedBy);
        Assert.Equal(4, thresholds.Values.Count);
        Assert.Equal(
            [124m, 154m, 344m, 394m],
            thresholds.Values.Select(t => t.ValueCm).ToArray());
        Assert.Equal("STANDBY_STAT", thresholds.Values[0].LabelOriginal);
    }

    [Fact]
    public void Vrijednost_ispod_najnizeg_praga_ne_mijenja_status_koji_izvor_tvrdi()
    {
        var parsed = ParseSample();

        var zenica = Assert.IsType<StationReading.Measured>(
            parsed.Readings.Single(r => r.Station.Name == "Bosna-Zenica"));

        // Vodostaj je duboko ispod najnižeg praga, a izvor i dalje kaže `Standby`.
        // Status se ne rekonstruiše iz vrijednosti i pragova — zlatno pravilo 3.
        Assert.True(zenica.MeasuredValue.ValueCm < zenica.Thresholds!.Values[0].ValueCm);
        Assert.Equal(AlertLevel.Normal, zenica.ClaimedLevel);
    }

    [Fact]
    public void Atribucija_putuje_sa_svakom_dionicom_a_ne_stoji_u_footeru()
    {
        var parsed = ParseSample();

        Assert.All(parsed.Readings, r =>
        {
            Assert.Equal("Agencija za vodno područje rijeke Save", r.Station.Attribution.AgencyName);
            Assert.Equal(new Uri("https://www.voda.ba"), r.Station.Attribution.AgencyUrl);
        });
    }

    [Fact]
    public void Arcgis_greska_u_tijelu_odgovora_se_ne_cita_kao_uspjeh()
    {
        // ArcGIS vraća greške sa HTTP 200 (SOURCES.md §1.5).
        const string body = """{"error":{"code":400,"message":"Failed to execute query."}}""";

        var ex = Assert.Throws<SourceResponseException>(() =>
            AvpSavaReachParser.Parse(body, Clock, Attribution, TimeSpan.FromHours(1)));

        Assert.Contains("400", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Neispravan_zapis_preskace_samo_sebe()
    {
        const string body = """
        {"features":[
          {"properties":{"SEC_ID":1,"description":"Dobra","CURRENT_STATUS":"Standby","H_CM":10,"DATE_TIME":1785873600000}},
          {"properties":{"description":"Bez kljuca"}}
        ]}
        """;

        var parsed = AvpSavaReachParser.Parse(body, Clock, Attribution, TimeSpan.FromHours(1));

        Assert.Single(parsed.Readings);
        Assert.Single(parsed.Skipped);
        Assert.Equal("Dobra", parsed.Readings[0].Station.Name);
    }

    [Fact]
    public void Nepoznat_status_daje_unknown_ali_zadrzava_vrijednost_i_original()
    {
        const string body = """
        {"features":[
          {"properties":{"SEC_ID":9,"description":"Nova rijeka","CURRENT_STATUS":"Nesto sasvim novo","H_CM":42.5,"DATE_TIME":1785873600000}}
        ]}
        """;

        var parsed = AvpSavaReachParser.Parse(body, Clock, Attribution, TimeSpan.FromHours(1));

        var reading = Assert.IsType<StationReading.Measured>(parsed.Readings.Single());
        Assert.Equal(AlertLevel.Unknown, reading.ClaimedLevel);
        Assert.Equal(42.5m, reading.MeasuredValue.ValueCm);
        Assert.Equal("Nesto sasvim novo", reading.StatusLabelOriginal);
        Assert.Equal(["Nesto sasvim novo"], parsed.UnrecognisedStatuses);
    }

    [Fact]
    public void Vrijednost_bez_vremena_mjerenja_nije_mjerenje()
    {
        const string body = """
        {"features":[
          {"properties":{"SEC_ID":3,"description":"Bez vremena","CURRENT_STATUS":"Standby","H_CM":88.0,"DATE_TIME":null}}
        ]}
        """;

        var parsed = AvpSavaReachParser.Parse(body, Clock, Attribution, TimeSpan.FromHours(1));

        var reading = Assert.IsType<StationReading.NoData>(parsed.Readings.Single());

        // Ovo je najoštriji slučaj u cijelom adapteru: izvor tvrdi `Standby`, što bi bilo
        // `Normal`, ali vremena mjerenja nema. Vrijednost bez vremena se ne smije prikazati,
        // pa tvrdnja izvora ne prolazi — ostaje `Unknown`.
        Assert.Equal(AlertLevel.Unknown, reading.Level);
        Assert.NotEqual(AlertLevel.Normal, reading.Level);
        Assert.Null(reading.Measurement);

        // Original se i dalje čuva, da se korisniku može reći šta je agencija rekla.
        Assert.Equal("Standby", reading.StatusLabelOriginal);
        Assert.Contains("DATE_TIME", reading.Reason, StringComparison.Ordinal);
    }
}
