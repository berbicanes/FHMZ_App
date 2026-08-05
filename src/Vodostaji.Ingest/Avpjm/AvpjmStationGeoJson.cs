using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Vodostaji.Core;

namespace Vodostaji.Ingest.Avpjm;

/// <summary>
/// Gradi GeoJSON sloj AVPJM-a — tačke, ne poligoni.
///
/// Zaseban graditelj, zaseban fajl, zasebna legenda. Dionice AVP Save i stanice AVPJM-a se
/// nikad ne stapaju: jug mora izgledati kao jug, sa svojom pričom o tome šta se zna a šta ne.
/// </summary>
public static class AvpjmStationGeoJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Build(
        SourceFetchResult result,
        DateTimeOffset now,
        IReadOnlyDictionary<string, PreviousMeasurement>? previousByKey = null)
    {
        var features = new JsonArray();
        var withoutCoordinates = 0;

        foreach (var reading in result.Readings)
        {
            if (reading.Station.Coordinates is not { } coordinates)
            {
                withoutCoordinates++;
                continue;
            }

            features.Add(new JsonObject
            {
                ["type"] = "Feature",
                ["geometry"] = new JsonObject
                {
                    ["type"] = "Point",
                    // GeoJSON traži lon pa lat — obrnuto od `location` polja izvora.
                    ["coordinates"] = new JsonArray(coordinates.Longitude, coordinates.Latitude),
                },
                ["properties"] = JsonSerializer.SerializeToNode(
                    Properties(reading, result.FetchedAt, now, Previous(previousByKey, reading)),
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
        DateTimeOffset fetchedAt,
        DateTimeOffset now,
        PreviousMeasurement? previous)
    {
        var measurement = reading.Measurement;
        var age = measurement?.AgeAt(now);
        var hasMeasurement = measurement is not null;

        return new ReachProperties
        {
            SourceId = reading.Station.SourceId,
            StationKey = reading.Station.StationKey,
            Name = reading.Station.Name,
            River = reading.Station.River,

            // Uvijek `Unknown` — agencija stupanj ne objavljuje (SOURCES.md §2.1).
            Level = reading.Level.ToString(),
            LevelLabel = AvpjmLegend.Label(hasMeasurement),
            Color = AvpjmLegend.Color(reading.Level, hasMeasurement),

            // Izvor ne šalje tekst statusa, pa je prazan string ovdje istina.
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

            PreviousValueCm = hasMeasurement ? previous?.ValueCm : null,
            PreviousMeasuredAt = hasMeasurement ? previous?.MeasuredAt : null,
            ChangeCm = measurement is not null && previous is not null
                ? measurement.ValueCm - previous.ValueCm
                : null,
            ChangeOverMinutes = measurement is not null && previous is not null
                ? (long)Math.Round((measurement.MeasuredAt - previous.MeasuredAt).TotalMinutes)
                : null,

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
