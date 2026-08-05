using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Vodostaji.Core;

namespace Vodostaji.Ingest;

/// <summary>Legenda jednog izvora. Svaki je donosi svoju.</summary>
public interface ISourceLegend
{
    string Color(StationReading reading);

    string Label(StationReading reading);
}

/// <summary>
/// Gradi GeoJSON sloj od tačaka.
///
/// Dijele ga izvori koji objavljuju stanice a ne dionice. **Dijeljenje koda nije stapanje
/// slojeva:** svaki izvor prosljeđuje vlastitu <see cref="ISourceLegend"/>, ide u vlastiti
/// fajl i crta se kao vlastiti sloj. Zajednički graditelj samo sprječava da se dva skoro
/// ista koda vremenom raziđu.
/// </summary>
public static class PointSourceGeoJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Build(
        SourceFetchResult result,
        ISourceLegend legend,
        DateTimeOffset now,
        IReadOnlyDictionary<string, PreviousMeasurement>? previousByKey = null)
    {
        var features = new JsonArray();
        var withoutCoordinates = 0;

        foreach (var reading in result.Readings)
        {
            if (reading.Station.Coordinates is not { } coordinates)
            {
                // Stanica bez koordinata ne može na mapu, ali se broji i objavljuje.
                withoutCoordinates++;
                continue;
            }

            features.Add(new JsonObject
            {
                ["type"] = "Feature",
                ["geometry"] = new JsonObject
                {
                    ["type"] = "Point",
                    // GeoJSON traži lon pa lat.
                    ["coordinates"] = new JsonArray(coordinates.Longitude, coordinates.Latitude),
                },
                ["properties"] = JsonSerializer.SerializeToNode(
                    Properties(reading, legend, result.FetchedAt, now, Previous(previousByKey, reading)),
                    Options),
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
            MeasuredCount = result.MeasuredCount,
            WithoutGeometry = withoutCoordinates,
        };

        return new JsonObject
        {
            ["type"] = "FeatureCollection",
            ["meta"] = JsonSerializer.SerializeToNode(meta, Options),
            ["features"] = features,
        }.ToJsonString(Options);
    }

    private static PreviousMeasurement? Previous(
        IReadOnlyDictionary<string, PreviousMeasurement>? previousByKey, StationReading reading) =>
        previousByKey is not null && previousByKey.TryGetValue(reading.Station.StationKey, out var p)
            ? p
            : null;

    private static ReachProperties Properties(
        StationReading reading,
        ISourceLegend legend,
        DateTimeOffset fetchedAt,
        DateTimeOffset now,
        PreviousMeasurement? previous)
    {
        var measurement = reading.Measurement;
        var age = measurement?.AgeAt(now);

        // Trend koji izvor objavljuje ima prednost nad našim izvodom iz dva očitanja.
        var published = (reading as StationReading.Measured)?.Trend;

        return new ReachProperties
        {
            SourceId = reading.Station.SourceId,
            StationKey = reading.Station.StationKey,
            Name = reading.Station.Name,
            River = reading.Station.River,

            Level = reading.Level.ToString(),
            LevelLabel = legend.Label(reading),
            Color = legend.Color(reading),

            StatusLabelOriginal = reading.StatusLabelOriginal,

            ValueCm = measurement?.ValueCm,
            MeasuredAt = measurement?.MeasuredAt,
            FetchedAt = fetchedAt,

            AgeMinutes = age is null ? null : (long)Math.Round(age.Value.TotalMinutes),
            ExpectedIntervalMinutes = (long)reading.Station.ExpectedInterval.TotalMinutes,
            PublicationLagMinutes = (long)reading.Station.TypicalPublicationLag.TotalMinutes,
            AgeRatio = reading.Station.MissedCycles(measurement?.MeasuredAt, now) is { } missed
                ? Math.Round(missed, 2)
                : null,

            PreviousValueCm = measurement is null ? null : previous?.ValueCm,
            PreviousMeasuredAt = measurement is null ? null : previous?.MeasuredAt,
            ChangeCm = measurement is not null && previous is not null
                ? measurement.ValueCm - previous.ValueCm
                : null,
            ChangeOverMinutes = measurement is not null && previous is not null
                ? (long)Math.Round((measurement.MeasuredAt - previous.MeasuredAt).TotalMinutes)
                : null,

            PublishedTrend = published?.Direction.ToString(),
            PublishedTrendLabel = published?.LabelOriginal,

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
