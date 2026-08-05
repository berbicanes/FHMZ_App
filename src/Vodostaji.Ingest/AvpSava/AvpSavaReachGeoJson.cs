using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Vodostaji.Core;

namespace Vodostaji.Ingest.AvpSava;

/// <summary>
/// Gradi GeoJSON koji mapa crta.
///
/// Namjerno je vezan za jedan izvor. Sljedeći izvor dobija svoj graditelj i svoj sloj, jer
/// stapanje agencija u jedan sloj sa jednom legendom je zabranjeno — jug mape mora izgledati
/// kao jug bez podatka, ne kao dio iste priče.
///
/// Svojstva se sklapaju u <see cref="ReachProperties"/> pa serijalizuju. Taj tip je jedini
/// izvor istine: iz njega ide OpenAPI shema, a iz nje TypeScript tipovi.
/// </summary>
public static class AvpSavaReachGeoJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    public static string Build(
        SourceFetchResult result,
        IReadOnlyDictionary<string, string> geometryByKey,
        DateTimeOffset now)
    {
        var features = new JsonArray();
        var withoutGeometry = 0;

        foreach (var reading in result.Readings)
        {
            if (!geometryByKey.TryGetValue(reading.Station.StationKey, out var geometry))
            {
                withoutGeometry++;
                continue;
            }

            features.Add(new JsonObject
            {
                ["type"] = "Feature",
                // Geometrija se ubacuje kao već serijalizovan tekst — poligon koji prođe
                // kroz naš model pa se ponovo ispiše je poligon koji smo mi prepisali.
                ["geometry"] = JsonNode.Parse(geometry),
                ["properties"] = JsonSerializer.SerializeToNode(
                    Properties(reading, result.FetchedAt, now), Options),
            });
        }

        var meta = new ReachMeta
        {
            SourceId = result.SourceId,
            FetchedAt = result.FetchedAt,
            GeneratedAt = now,
            ReachCount = result.Readings.Count,
            KnownCount = result.KnownCount,
            UnknownCount = result.UnknownCount,
            WithoutGeometry = withoutGeometry,
        };

        var collection = new JsonObject
        {
            ["type"] = "FeatureCollection",
            ["meta"] = JsonSerializer.SerializeToNode(meta, Options),
            ["features"] = features,
        };

        return collection.ToJsonString(Options);
    }

    private static ReachProperties Properties(
        StationReading reading, DateTimeOffset fetchedAt, DateTimeOffset now)
    {
        var measurement = reading.Measurement;
        var age = measurement?.AgeAt(now);

        return new ReachProperties
        {
            SourceId = reading.Station.SourceId,
            StationKey = reading.Station.StationKey,
            Name = reading.Station.Name,
            River = reading.Station.River,

            Level = reading.Level.ToString(),
            LevelLabel = AvpSavaLegend.Label(reading.Level),
            Color = AvpSavaLegend.Color(reading.Level),

            // Doslovni tekst agencije putuje do browsera. Bez njega korisniku možemo
            // pokazati samo naš prevod njihove tvrdnje.
            StatusLabelOriginal = reading.StatusLabelOriginal,

            ValueCm = measurement?.ValueCm,
            MeasuredAt = measurement?.MeasuredAt,
            FetchedAt = fetchedAt,

            AgeMinutes = age is null ? null : (long)Math.Round(age.Value.TotalMinutes),
            ExpectedIntervalMinutes = (long)reading.Station.ExpectedInterval.TotalMinutes,
            PublicationLagMinutes = (long)reading.Station.TypicalPublicationLag.TotalMinutes,

            // Broj propuštenih ciklusa, mjeren od trenutka kad je podatak realno mogao stići.
            // UI.md §2 dijeli prikaz na <1×, 1–3× i >3×, pa mu se daje broj umjesto gotove
            // ocjene — prag prikaza je odluka UI-a.
            AgeRatio = reading.Station.MissedCycles(measurement?.MeasuredAt, now) is { } missed
                ? Math.Round(missed, 2)
                : null,

            // Atribucija po dionici, ne u footeru (LEGAL.md §2.1).
            AgencyName = reading.Station.Attribution.AgencyName,
            AgencyUrl = reading.Station.Attribution.AgencyUrl.ToString(),
            SourceUrl = reading.Station.Attribution.SourceUrl?.ToString(),

            NoDataReason = reading is StationReading.NoData noData ? noData.Reason : null,

            Thresholds = reading.Thresholds is { IsEmpty: false } thresholds
                ? [.. thresholds.Values.Select(t =>
                    new ReachThreshold(t.LabelOriginal, t.ValueCm, t.Level?.ToString()))]
                : null,
            ThresholdsDefinedBy = reading.Thresholds is { IsEmpty: false } defined
                ? defined.DefinedBy
                : null,
        };
    }
}
