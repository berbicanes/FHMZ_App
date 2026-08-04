using System.Text.Json;

namespace Vodostaji.Ingest.AvpSava;

/// <summary>
/// Povlači geometriju dionica, odvojeno od vrijednosti.
///
/// Odvojeno je namjerno: poligoni dionica se mijenjaju rijetko, a vrijednosti svakih sat.
/// Povlačenje geometrije uz svaki ciklus bi značilo desetine puta veći odgovor bez ijedne
/// nove informacije — a njihova infrastruktura je javna imovina (zlatno pravilo 6).
/// </summary>
public sealed class AvpSavaGeometrySource(HttpClient httpClient)
{
    /// <summary>
    /// `geometryPrecision=5` i `maxAllowableOffset=0.0005` traže od servera da poligone
    /// pojednostavi i zaokruži prije slanja.
    ///
    /// Izmjereno 2026-08-04 na svih 45 dionica: bez parametara 3.63 MB (1.46 MB gzip),
    /// sa njima **239 KB (72 KB gzip)** — petnaest puta manje. Offset od 0.0005 stepeni je
    /// oko 55 metara, što se na mapi države ne vidi, a aplikacija mora biti brza na slaboj
    /// vezi (CLAUDE.md → Šta NE raditi).
    ///
    /// Ovo je odluka o **prikazu**, ne o podatku: mijenja se oblik linije, nikad vrijednost,
    /// status ni vrijeme. Generalizaciju radi njihov server, pa je i odgovor koji šalju manji.
    /// </summary>
    private const string Url =
        "https://isvportal.voda.ba/server/rest/services/Hidrolosko_stane_u_realnom_vremenu/" +
        "FeatureServer/0/query?where=1%3D1&outFields=SEC_ID&outSR=4326&returnGeometry=true" +
        "&geometryPrecision=5&maxAllowableOffset=0.0005&f=geojson";

    /// <summary>Geometrija se osvježava jednom dnevno. Češće nema šta da se promijeni.</summary>
    public static TimeSpan RefreshInterval => TimeSpan.FromHours(24);

    /// <summary>
    /// Vraća geometriju po `SEC_ID`, kao sirovi GeoJSON tekst.
    ///
    /// Geometrija se **ne parsira u domenske tipove**. Poligon koji prođe kroz naš model pa
    /// se ponovo serijalizuje je poligon koji smo mi prepisali; ovako do mape stiže tačno
    /// ono što je agencija poslala.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> FetchAsync(CancellationToken cancellationToken)
    {
        var body = await httpClient.GetStringAsync(Url, cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(body);

        if (document.RootElement.TryGetProperty("error", out var error))
        {
            throw new SourceResponseException($"ArcGIS greška u tijelu odgovora: {error.GetRawText()}");
        }

        if (!document.RootElement.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array)
        {
            throw new SourceResponseException("Odgovor sa geometrijom nema niz `features`.");
        }

        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("properties", out var properties) ||
                !properties.TryGetProperty("SEC_ID", out var secId) ||
                secId.ValueKind != JsonValueKind.Number ||
                !feature.TryGetProperty("geometry", out var geometry) ||
                geometry.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            var key = secId.TryGetDecimal(out var number)
                ? number.ToString("0.################", System.Globalization.CultureInfo.InvariantCulture)
                : secId.GetRawText();

            byKey[key] = geometry.GetRawText();
        }

        return byKey;
    }
}
