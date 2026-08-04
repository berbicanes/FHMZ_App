using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vodostaji.Core;

namespace Vodostaji.Ingest.AvpSava;

/// <summary>
/// Gradi GeoJSON koji mapa crta.
///
/// Namjerno je vezan za jedan izvor. Sljedeći izvor dobija svoj graditelj i svoj sloj, jer
/// stapanje agencija u jedan sloj sa jednom legendom je zabranjeno — jug mape mora izgledati
/// kao jug bez podatka, ne kao dio iste priče.
///
/// Sve što UI treba da bude pošten je **u fajlu**: vrijeme mjerenja, starost, ime agencije,
/// link, i razlog kad podatka nema. Ništa od toga se ne dograđuje u browseru.
/// </summary>
public static class AvpSavaReachGeoJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
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
                ["geometry"] = JsonNode.Parse(geometry),
                ["properties"] = Properties(reading, result.FetchedAt, now),
            });
        }

        var collection = new JsonObject
        {
            ["type"] = "FeatureCollection",
            // Meta ide u fajl da bi UI mogao reći "podaci od …" bez zasebnog poziva,
            // i da bi se u browseru vidjelo koliko dionica nema geometriju.
            ["meta"] = new JsonObject
            {
                ["sourceId"] = result.SourceId,
                ["fetchedAt"] = result.FetchedAt.ToString("O", CultureInfo.InvariantCulture),
                ["generatedAt"] = now.ToString("O", CultureInfo.InvariantCulture),
                ["reachCount"] = result.Readings.Count,
                ["knownCount"] = result.KnownCount,
                ["unknownCount"] = result.UnknownCount,
                ["withoutGeometry"] = withoutGeometry,
            },
            ["features"] = features,
        };

        return collection.ToJsonString(Options);
    }

    private static JsonObject Properties(
        StationReading reading, DateTimeOffset fetchedAt, DateTimeOffset now)
    {
        var measurement = reading.Measurement;

        var properties = new JsonObject
        {
            ["sourceId"] = reading.Station.SourceId,
            ["stationKey"] = reading.Station.StationKey,
            ["name"] = reading.Station.Name,
            ["river"] = reading.Station.River,

            ["level"] = reading.Level.ToString(),
            ["levelLabel"] = AvpSavaLegend.Label(reading.Level),
            ["color"] = AvpSavaLegend.Color(reading.Level),

            // Doslovni tekst agencije putuje do browsera. Bez njega korisniku možemo
            // pokazati samo naš prevod njihove tvrdnje.
            ["statusLabelOriginal"] = reading.StatusLabelOriginal,

            ["valueCm"] = measurement is null ? null : JsonValue.Create(measurement.ValueCm),

            // Vrijeme mjerenja i vrijeme dohvata su dva odvojena polja i u fajlu.
            ["measuredAt"] = measurement?.MeasuredAt.ToString("O", CultureInfo.InvariantCulture),
            ["fetchedAt"] = fetchedAt.ToString("O", CultureInfo.InvariantCulture),

            // Starost ide gotova, da UI ne mora računati i da ne može pogriješiti u računu.
            ["ageMinutes"] = measurement is null
                ? null
                : JsonValue.Create((long)Math.Round(measurement.AgeAt(now).TotalMinutes)),

            ["expectedIntervalMinutes"] = (long)reading.Station.ExpectedInterval.TotalMinutes,
            ["isStale"] = measurement is not null &&
                          measurement.AgeAt(now) > reading.Station.ExpectedInterval * 2,

            // Atribucija po dionici, ne u footeru (LEGAL.md §2.1).
            ["agencyName"] = reading.Station.Attribution.AgencyName,
            ["agencyUrl"] = reading.Station.Attribution.AgencyUrl.ToString(),
            ["sourceUrl"] = reading.Station.Attribution.SourceUrl?.ToString(),

            ["noDataReason"] = reading is StationReading.NoData noData ? noData.Reason : null,
        };

        if (reading.Thresholds is { IsEmpty: false } thresholds)
        {
            var list = new JsonArray();
            foreach (var threshold in thresholds.Values)
            {
                list.Add(new JsonObject
                {
                    ["label"] = threshold.LabelOriginal,
                    ["valueCm"] = JsonValue.Create(threshold.ValueCm),
                    ["level"] = threshold.Level?.ToString(),
                });
            }

            properties["thresholds"] = list;
            properties["thresholdsDefinedBy"] = thresholds.DefinedBy;
        }

        return properties;
    }
}
