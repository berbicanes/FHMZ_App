using System.Globalization;
using System.Text.Json;
using Vodostaji.Core;

namespace Vodostaji.Ingest.Wiski;

/// <summary>Jedan red iz jednog sloja WISKI izvoza, prije spajanja po stanici.</summary>
public sealed record WiskiRow
{
    public required string StationNo { get; init; }
    public required string StationName { get; init; }
    public string? River { get; init; }
    public Coordinates? Coordinates { get; init; }
    public required ObservationParameter Parameter { get; init; }
    public required string ParameterLabel { get; init; }
    public required decimal Value { get; init; }
    public required string Unit { get; init; }
    public required DateTimeOffset MeasuredAt { get; init; }

    /// <summary>Doslovna klasa iz izvora (`#MIN#`, `#TH1#`). Iz nje se **ništa ne izvodi**.</summary>
    public string? WaterLevelClass { get; init; }
}

public sealed record ParsedWiskiLayer
{
    public required IReadOnlyList<WiskiRow> Rows { get; init; }
    public required IReadOnlyList<SkippedStation> Skipped { get; init; }
}

/// <summary>
/// Čita jedan sloj statičkog WISKI izvoza AVP Save (SOURCES.md §4.5).
///
/// <para>
/// Format je ravna JSON lista, jedan objekat po stanici, sa zadnjom vrijednošću. Nema
/// ugnježđivanja i nema paginacije — cijela složenost je u tome **šta se odbacuje**.
/// </para>
/// </summary>
public static class WiskiParser
{
    /// <summary>
    /// Oznaka parametra iz `L1_stationparameter_no`.
    ///
    /// Nepoznata oznaka daje <see cref="ObservationParameter.Unknown"/>, a red se **ne
    /// odbacuje**: vrijednost i jedinica se i dalje prikazuju pod imenom koje im izvor daje.
    /// Odbaciti mjerenje zato što mu ne znamo ime značilo bi sakriti podatak koji postoji.
    /// </summary>
    private static ObservationParameter ParameterOf(string? code) => code switch
    {
        "H" => ObservationParameter.WaterLevel,
        "Q" => ObservationParameter.Flow,
        "WT" => ObservationParameter.WaterTemperature,
        "AT" => ObservationParameter.AirTemperature,
        "Precip" => ObservationParameter.Precipitation,
        "GWH" => ObservationParameter.GroundwaterLevel,
        "GWT" => ObservationParameter.GroundwaterTemperature,
        _ => ObservationParameter.Unknown,
    };

    public static ParsedWiskiLayer ParseLayer(string json, SourceClock clock)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new SourceResponseException(
                "Sloj nije JSON lista. Izvoz je vjerovatno promijenjen ili je vraćena greška "
                + "umjesto podataka.");
        }

        var rows = new List<WiskiRow>();
        var skipped = new List<SkippedStation>();

        foreach (var element in document.RootElement.EnumerateArray())
        {
            var name = Text(element, "metadata_station_name") ?? "";
            var no = Text(element, "metadata_station_no");

            if (no is null)
            {
                skipped.Add(new SkippedStation(name, "Nedostaje `metadata_station_no`."));
                continue;
            }

            // Vrijednost bez vremena se ne smije prikazati (zlatno pravilo 2), a vrijeme bez
            // vrijednosti nije mjerenje. U slojevima 80 i 90 je to većina redova — zato ti
            // slojevi i ne ulaze u izvor (SOURCES.md §4.5).
            var rawTime = Text(element, "L1_timestamp");
            var rawValue = Text(element, "L1_ts_value");

            if (rawTime is null || rawValue is null)
            {
                skipped.Add(new SkippedStation(
                    name,
                    rawTime is null ? "Red nema `L1_timestamp`." : "Red nema `L1_ts_value`."));
                continue;
            }

            if (!DateTimeOffset.TryParse(
                    rawTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
            {
                skipped.Add(new SkippedStation(name, $"Vrijeme `{rawTime}` se ne da pročitati."));
                continue;
            }

            if (!decimal.TryParse(
                    rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                skipped.Add(new SkippedStation(name, $"Vrijednost `{rawValue}` nije broj."));
                continue;
            }

            var code = Text(element, "L1_stationparameter_no");

            rows.Add(new WiskiRow
            {
                StationNo = no,
                StationName = name.Length > 0 ? name : no,
                River = Text(element, "metadata_river_name"),
                Coordinates = ReadCoordinates(element),
                Parameter = ParameterOf(code),
                ParameterLabel =
                    Text(element, "L1_stationparameter_name")
                    ?? Text(element, "L1_label")
                    ?? code
                    ?? "Nepoznat parametar",
                Value = value,
                Unit = Text(element, "L1_ts_unitsymbol") ?? "",
                // Pomak je u samoj vrijednosti, pa se ništa ne rekonstruiše.
                MeasuredAt = clock.ResolveExplicit(instant),
                WaterLevelClass = Text(element, "L1_web_waterlevel_class"),
            });
        }

        if (rows.Count == 0 && skipped.Count == 0)
        {
            throw new SourceResponseException("Sloj je prazan — nijedan red, ni preskočen.");
        }

        return new ParsedWiskiLayer { Rows = rows, Skipped = skipped };
    }

    /// <summary>
    /// Koordinate su decimalni stepeni u tekstu. Prazan string je legitiman i znači
    /// „stanica bez koordinata", ne nulu — nula bi je poslala u Gvinejski zaljev.
    /// </summary>
    private static Coordinates? ReadCoordinates(JsonElement element)
    {
        var lat = Text(element, "metadata_station_latitude");
        var lon = Text(element, "metadata_station_longitude");

        if (lat is null || lon is null) return null;

        if (!double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !double.TryParse(lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
        {
            return null;
        }

        // BiH je unutar ovog okvira sa rezervom. Koordinata izvan njega je greška u izvozu,
        // a stanica u pogrešnoj zemlji je gora od stanice bez koordinata: prva se crta.
        if (latitude is < 41 or > 47 || longitude is < 14 or > 21) return null;

        return new Coordinates(latitude, longitude);
    }

    private static string? Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property)) return null;

        var value = property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
