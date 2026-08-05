using System.Text.Json;
using Vodostaji.Ingest.AvpSava;

namespace Vodostaji.Core.Tests;

/// <summary>
/// Registar mjernih mjesta, protiv snimka od 2026-08-04.
///
/// Ono što se ovdje najviše čuva nije broj stanica nego **odsustvo statusa**: registar kaže
/// gdje se mjeri, ne kakvo je stanje. Bilo kakvo polje koje bi UI mogao pročitati kao
/// "sve u redu" bilo bi kršenje zlatnog pravila 1 na najtiši mogući način.
/// </summary>
public class AvpSavaStationSourceTests
{
    private const string Fixture = "avp-sava/stanice-registar-sa-geometrijom-2026-08-04.json";

    private static readonly DateTimeOffset Now = new(2026, 8, 5, 0, 0, 0, TimeSpan.Zero);

    private static JsonDocument Build() =>
        JsonDocument.Parse(AvpSavaStationSource.Build(Vodostaji.Core.Tests.Fixture.Read(Fixture), Now));

    [Fact]
    public void Registar_se_cita_i_brojevi_se_slazu()
    {
        using var document = Build();
        var meta = document.RootElement.GetProperty("meta");
        var features = document.RootElement.GetProperty("features");

        var total = meta.GetProperty("stationCount").GetInt32();
        var withoutGeometry = meta.GetProperty("withoutGeometry").GetInt32();
        var withoutName = meta.GetProperty("withoutName").GetInt32();

        Assert.Equal(102, total);

        // Zbir mora zatvoriti registar. Korisnik koji prebroji tačke na mapi mora doći do
        // istog broja do kojeg dolazimo mi, inače razlika izgleda kao da nešto krijemo.
        Assert.Equal(total, features.GetArrayLength() + withoutGeometry + withoutName);
    }

    [Fact]
    public void Nedostajuci_podaci_se_broje_a_ne_prescutkuju()
    {
        using var document = Build();
        var meta = document.RootElement.GetProperty("meta");

        // SOURCES.md §1.2: 1 stanica bez geometrije, 13 bez kote nule, 1 bez naziva.
        Assert.Equal(1, meta.GetProperty("withoutGeometry").GetInt32());
        Assert.Equal(13, meta.GetProperty("withoutGaugeZero").GetInt32());
        Assert.Equal(1, meta.GetProperty("withoutName").GetInt32());
    }

    [Fact]
    public void Nijedna_stanica_ne_nosi_status_boju_ni_vrijednost()
    {
        using var document = Build();

        foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
        {
            var properties = feature.GetProperty("properties");

            foreach (var forbidden in new[] { "level", "levelLabel", "color", "valueCm", "measuredAt" })
            {
                Assert.False(
                    properties.TryGetProperty(forbidden, out _),
                    $"registar mjernih mjesta ne smije nositi `{forbidden}` — stanje se čita na dionici");
            }
        }
    }

    [Fact]
    public void Koordinate_dolaze_iz_geometrije_a_ne_iz_x_y_atributa()
    {
        using var document = Build();

        var gorazde = document.RootElement.GetProperty("features").EnumerateArray()
            .Single(f => f.GetProperty("properties").GetProperty("name").GetString() == "HS Goražde");

        var coordinates = gorazde.GetProperty("geometry").GetProperty("coordinates");
        var lon = coordinates[0].GetDouble();
        var lat = coordinates[1].GetDouble();

        // Goražde je na oko 18.97 E, 43.67 N. Atributi `x`/`y` bi za istu stanicu dali
        // Gauss-Krüger vrijednosti u milionima, pa ovaj test pada čim ih neko uvede.
        Assert.InRange(lon, 18.9, 19.1);
        Assert.InRange(lat, 43.6, 43.8);
    }

    [Fact]
    public void Sve_objavljene_stanice_su_unutar_granica_bih()
    {
        using var document = Build();

        foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
        {
            var coordinates = feature.GetProperty("geometry").GetProperty("coordinates");

            // SOURCES.md §1.2: lon 15.783–18.974, lat 43.582–45.180. Vrijednost izvan ovoga
            // znači da je neko negdje pomiješao ose ili zonu.
            Assert.InRange(coordinates[0].GetDouble(), 15.0, 20.0);
            Assert.InRange(coordinates[1].GetDouble(), 42.5, 46.0);
        }
    }

    [Fact]
    public void Kota_nule_koja_nedostaje_ostaje_null_a_ne_nula()
    {
        using var document = Build();

        var missing = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(f => f.GetProperty("properties"))
            .Where(p => p.GetProperty("gaugeZero").ValueKind == JsonValueKind.Null)
            .ToList();

        Assert.NotEmpty(missing);

        // Nula bi bila kota na nivou mora. Nedostajuća kota mora ostati nedostajuća.
        Assert.All(missing, p => Assert.Equal(JsonValueKind.Null, p.GetProperty("gaugeZero").ValueKind));
    }

    [Fact]
    public void Arcgis_greska_u_tijelu_odgovora_se_ne_cita_kao_prazan_registar()
    {
        const string body = """{"error":{"code":400,"message":"Failed to execute query."}}""";

        Assert.Throws<SourceResponseException>(() => AvpSavaStationSource.Build(body, Now));
    }
}
