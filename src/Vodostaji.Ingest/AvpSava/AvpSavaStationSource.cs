using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Vodostaji.Core;
using Vodostaji.Ingest;

namespace Vodostaji.Ingest.AvpSava;

/// <summary>
/// Registar hidroloških stanica — `ISV_BIH_2009_javnakarta/MapServer/1`.
///
/// Odvojen od sloja dionica i **nije spojen s njim**: `HYDRO_ID` na dionicama ne pokazuje na
/// ovaj registar (SOURCES.md §1.7). Ovo je sloj mjernih mjesta, ne sloj stanja.
///
/// Registar se mijenja rijetko, pa se povlači jednom dnevno.
/// </summary>
public sealed class AvpSavaStationSource(HttpClient httpClient)
{
    /// <summary>
    /// **`outFields=*` ruši ovaj sloj** (SOURCES.md §1.2): prijavljuje `OBJECTID` koji ne može
    /// servirati, uz `objectIdField: null`. Polja se zato traže poimence, bez njega.
    /// Dijakritika u `TIP_HIDROLOŠKE_STANICE` mora biti procentno kodirana.
    /// </summary>
    private const string Url =
        "https://isvportal.voda.ba/server/rest/services/ISV_BIH_2009_javnakarta/MapServer/1/query" +
        "?where=1%3D1&outFields=HID_ID,NAZIV,LOKACIJA,TIP_HIDROLO%C5%A0KE_STANICE,KOTA_0,BR_V_LETVI" +
        "&outSR=4326&returnGeometry=true&geometryPrecision=5&f=geojson";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static TimeSpan RefreshInterval => TimeSpan.FromHours(24);

    public async Task<string> FetchGeoJsonAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var body = await httpClient.GetStringAsync(Url, cancellationToken).ConfigureAwait(false);
        return Build(body, now);
    }

    /// <summary>Čista funkcija nad tekstom, testirana protiv snimljenog fixture-a.</summary>
    public static string Build(string body, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(body);

        if (document.RootElement.TryGetProperty("error", out var error))
        {
            throw new SourceResponseException($"ArcGIS greška u tijelu odgovora: {error.GetRawText()}");
        }

        if (!document.RootElement.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array)
        {
            throw new SourceResponseException("Registar stanica nema niz `features`.");
        }

        var output = new JsonArray();
        var total = 0;
        var withoutGeometry = 0;
        var withoutGaugeZero = 0;
        var withoutName = 0;

        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("properties", out var p) || p.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            total++;

            // Brojači idu prije svakog `continue`. Statistika mora opisivati **cijeli**
            // registar, ne samo dio koji je prošao do mape — inače meta laže o tome
            // koliko stanica uopšte postoji.
            if (Decimal(p, "KOTA_0") is null)
            {
                withoutGaugeZero++;
            }

            var name = String(p, "NAZIV");
            if (name is null)
            {
                withoutName++;
                continue;
            }

            // Geometrija je jedini pouzdan izvor koordinata. Atributi `x`/`y` miješaju
            // Gauss-Krüger zone i kod tri stanice imaju zamijenjene ose (SOURCES.md §1.2),
            // pa se ne traže ni ne koriste.
            var hasGeometry = feature.TryGetProperty("geometry", out var geometry) &&
                              geometry.ValueKind == JsonValueKind.Object;

            if (!hasGeometry)
            {
                // Stanica bez geometrije ne može na mapu, ali se broji i objavljuje.
                withoutGeometry++;
                continue;
            }

            output.Add(new JsonObject
            {
                ["type"] = "Feature",
                ["geometry"] = JsonNode.Parse(geometry.GetRawText()),
                ["properties"] = JsonSerializer.SerializeToNode(
                    new StationProperties
                    {
                        SourceId = AvpSavaArcGisSource.Id,
                        StationKey = Key(p),
                        Name = name,
                        Location = String(p, "LOKACIJA"),
                        StationType = String(p, "TIP_HIDROLOŠKE_STANICE"),
                        GaugeZero = Decimal(p, "KOTA_0"),
                        GaugeBoardCount = Int(p, "BR_V_LETVI"),
                        AgencyName = "Agencija za vodno područje rijeke Save",
                        AgencyUrl = "https://www.voda.ba",
                    },
                    Options),
            });
        }

        var meta = new StationMeta
        {
            SourceId = AvpSavaArcGisSource.Id,
            FetchedAt = now,
            StationCount = total,
            WithoutGeometry = withoutGeometry,
            WithoutGaugeZero = withoutGaugeZero,
            WithoutName = withoutName,
        };

        return new JsonObject
        {
            ["type"] = "FeatureCollection",
            ["meta"] = JsonSerializer.SerializeToNode(meta, Options),
            ["features"] = output,
        }.ToJsonString(Options);
    }

    private static string? Key(JsonElement properties) =>
        properties.TryGetProperty("HID_ID", out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetDecimal(out var number)
            ? number.ToString("0.################", CultureInfo.InvariantCulture)
            : null;

    private static string? String(JsonElement properties, string name) =>
        properties.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static decimal? Decimal(JsonElement properties, string name) =>
        properties.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetDecimal(out var number)
            ? number
            : null;

    private static int? Int(JsonElement properties, string name) =>
        properties.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : null;
}
