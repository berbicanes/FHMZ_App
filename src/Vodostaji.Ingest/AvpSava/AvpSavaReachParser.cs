using System.Globalization;
using System.Text.Json;
using Vodostaji.Core;
using Vodostaji.Ingest;


namespace Vodostaji.Ingest.AvpSava;

/// <summary>Rezultat parsiranja jednog odgovora, bez ijednog mrežnog poziva.</summary>
public sealed record ParsedReaches
{
    public required IReadOnlyList<StationReading> Readings { get; init; }

    public required IReadOnlyList<SkippedStation> Skipped { get; init; }

    /// <summary>Statusi koje smo dobili a nisu u rječniku od 2026-08-04. Prazna lista je
    /// očekivano stanje; bilo šta u njoj znači da je izvor promijenio rječnik i da se
    /// <see cref="AvpSavaStatusMap"/> mora dopuniti prije nego ti zapisi postanu upotrebljivi.</summary>
    public required IReadOnlyList<string> UnrecognisedStatuses { get; init; }
}

/// <summary>
/// Prevodi odgovor sloja `Hidrolosko_stane_u_realnom_vremenu/FeatureServer/0` u domenske tipove.
///
/// Čista funkcija nad tekstom — ne zna za HttpClient, pa se testira protiv snimljenih fixtura
/// bez mreže i bez ijednog kontejnera.
/// </summary>
public static class AvpSavaReachParser
{
    /// <summary>
    /// Pragovi kako ih sloj imenuje, sa stupnjem iz njihovog vlastitog aliasa
    /// (`Standby status (cm)`, `Regular defence status (cm)`, …).
    ///
    /// Napomena iz podataka: dionica `Bosna-Zenica` je 2026-08-04 imala `H_CM` 17.7 uz
    /// `STANDBY_STAT` 124 i status `Standby`. Vrijednost je dakle **ispod** najnižeg praga a
    /// status je i dalje najniži — što znači da se status **ne može** rekonstruisati iz
    /// vrijednosti i pragova. Zlatno pravilo 3 nije samo načelo nego i jedini ispravan način
    /// da se ovaj izvor pročita.
    /// </summary>
    private static readonly (string Field, AlertLevel Level)[] ThresholdFields =
    [
        ("STANDBY_STAT", AlertLevel.Normal),
        ("REGULAR_DEF_ST", AlertLevel.Elevated),
        ("OUTSTANDING_ST", AlertLevel.Flood),
        ("EMERGENCY_ST", AlertLevel.Emergency),
    ];

    public static ParsedReaches Parse(
        string body,
        SourceClock clock,
        Attribution attribution,
        TimeSpan expectedInterval,
        TimeSpan publicationLag = default)
    {
        var readings = new List<StationReading>();
        var skipped = new List<SkippedStation>();
        var unrecognised = new SortedSet<string>(StringComparer.Ordinal);

        using var document = JsonDocument.Parse(body);

        if (document.RootElement.TryGetProperty("error", out var error))
        {
            // ArcGIS greške stižu sa HTTP 200 (SOURCES.md §1.5). Ko gleda samo statusni kod
            // vidi uspjeh gdje ga nema.
            throw new SourceResponseException(
                $"ArcGIS greška u tijelu odgovora: {error.GetRawText()}");
        }

        if (!document.RootElement.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array)
        {
            throw new SourceResponseException("Odgovor nema niz `features`.");
        }

        foreach (var feature in features.EnumerateArray())
        {
            var attributes = Attributes(feature);
            if (attributes is null)
            {
                skipped.Add(new SkippedStation("?", "Zapis nema ni `properties` ni `attributes`."));
                continue;
            }

            var key = Key(attributes.Value);
            if (key is null)
            {
                skipped.Add(new SkippedStation("?", "Zapis nema `SEC_ID`."));
                continue;
            }

            try
            {
                readings.Add(ReadOne(
                    attributes.Value, key, clock, attribution, expectedInterval, publicationLag, unrecognised));
            }
            catch (Exception ex) when (ex is not SourceResponseException)
            {
                // Kontrolna lista: neuspjeh parsiranja preskače stanicu i logira se,
                // ne ruši cijeli run.
                skipped.Add(new SkippedStation(key, ex.Message));
            }
        }

        return new ParsedReaches
        {
            Readings = readings,
            Skipped = skipped,
            UnrecognisedStatuses = [.. unrecognised],
        };
    }

    private static StationReading ReadOne(
        JsonElement attributes,
        string key,
        SourceClock clock,
        Attribution attribution,
        TimeSpan expectedInterval,
        TimeSpan publicationLag,
        SortedSet<string> unrecognised)
    {
        var status = String(attributes, "CURRENT_STATUS");
        var station = BuildStation(attributes, key, attribution, expectedInterval, publicationLag);
        var thresholds = BuildThresholds(attributes, attribution.AgencyName);
        var label = status ?? "";

        if (status is not null && !AvpSavaStatusMap.IsRecognised(status))
        {
            unrecognised.Add(status);
        }

        if (AvpSavaStatusMap.MeansNoData(status))
        {
            return NoData(station, label, "Izvor je poslao status `No Data`.", thresholds);
        }

        var epoch = Epoch(attributes, "DATE_TIME");
        var value = Decimal(attributes, "H_CM");

        // Vrijednost bez vremena mjerenja se ne smije prikazati (zlatno pravilo 2), a vrijeme
        // bez vrijednosti nije mjerenje. Oba slučaja su NoData, sa različitim razlogom.
        if (epoch is null && value is null)
        {
            return NoData(station, label, "Nema ni `H_CM` ni `DATE_TIME`.", thresholds);
        }

        if (epoch is null)
        {
            return NoData(station, label, "`DATE_TIME` je null — vrijednost bez vremena mjerenja.", thresholds);
        }

        if (value is null)
        {
            return NoData(station, label, "`H_CM` je null.", thresholds);
        }

        return new StationReading.Measured
        {
            Station = station,
            StatusLabelOriginal = label,
            Thresholds = thresholds,
            ClaimedLevel = AvpSavaStatusMap.ToAlertLevel(status),
            MeasuredValue = new Measurement(value.Value, clock.ResolveEpochMilliseconds(epoch.Value)),
        };
    }

    private static StationReading NoData(
        Station station, string label, string reason, Thresholds thresholds) =>
        new StationReading.NoData
        {
            Station = station,
            StatusLabelOriginal = label,
            Reason = reason,
            Thresholds = thresholds,
        };

    private static Station BuildStation(
        JsonElement attributes,
        string key,
        Attribution attribution,
        TimeSpan expectedInterval,
        TimeSpan publicationLag)
    {
        var description = String(attributes, "description");

        // `description` je oblika `Rijeka-Mjesto` (`Bosna-Zenica`), ali ne uvijek —
        // `Fojnička rijeka` nema crticu. Rijeka se izvlači samo kad je oblik nedvosmislen.
        string? river = null;
        if (description is not null)
        {
            var dash = description.IndexOf('-', StringComparison.Ordinal);
            if (dash > 0 && dash < description.Length - 1)
            {
                river = description[..dash].Trim();
            }
        }

        return new Station
        {
            SourceId = AvpSavaArcGisSource.Id,
            StationKey = key,
            Name = description ?? key,
            River = river,
            // Geometrija dionice je poligon, ne tačka. Centroid bi bio naš izum, pa ga nema —
            // poligoni idu u mapu kao zaseban GeoJSON sloj, netaknuti.
            Coordinates = null,
            ExpectedInterval = expectedInterval,
            TypicalPublicationLag = publicationLag,
            Attribution = attribution,
        };
    }

    private static Thresholds BuildThresholds(JsonElement attributes, string definedBy)
    {
        var values = new List<Threshold>();

        foreach (var (field, level) in ThresholdFields)
        {
            if (Decimal(attributes, field) is { } value)
            {
                values.Add(new Threshold(field, value, level));
            }
        }

        return new Thresholds { Values = values, DefinedBy = definedBy };
    }

    private static JsonElement? Attributes(JsonElement feature)
    {
        if (feature.TryGetProperty("properties", out var properties) &&
            properties.ValueKind == JsonValueKind.Object)
        {
            return properties;
        }

        // ArcGIS `f=json` umjesto `f=geojson` — MapServer slojevi ne serviraju uvijek geoJSON.
        return feature.TryGetProperty("attributes", out var attributes) &&
               attributes.ValueKind == JsonValueKind.Object
            ? attributes
            : null;
    }

    private static string? Key(JsonElement attributes)
    {
        if (!attributes.TryGetProperty("SEC_ID", out var value) ||
            value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        // `SEC_ID` je Double u shemi ali cio broj u podacima. Ključ mora biti stabilan tekst,
        // pa `1` nikad ne smije postati `1.0` u jednom runu i `1` u sljedećem.
        return value.TryGetDecimal(out var number)
            ? number.ToString("0.################", CultureInfo.InvariantCulture)
            : value.GetRawText();
    }

    private static string? String(JsonElement attributes, string name) =>
        attributes.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// Čita broj u `decimal` direktno iz teksta odgovora. Nikad preko `double`:
    /// `H_CM` je `esriFieldTypeSingle` i stiže kao `17.6000004`, i taj artefakt se
    /// niti pojačava niti krije — prikazuje se ono što je izvor poslao.
    /// </summary>
    private static decimal? Decimal(JsonElement attributes, string name) =>
        attributes.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDecimal(out var number)
            ? number
            : null;

    private static long? Epoch(JsonElement attributes, string name) =>
        attributes.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var epoch)
            ? epoch
            : null;
}
