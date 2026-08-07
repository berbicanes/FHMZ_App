using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Vodostaji.Core;

namespace Vodostaji.Ingest.Fhmzbih;

/// <summary>Podaci sa podstranice jedne stanice — mijenjaju se rijetko.</summary>
public sealed record FhmzbihStationDetails(
    string Name,
    Coordinates? Coordinates,
    decimal? GaugeZero,
    string? River,
    string? Basin);

public sealed record ParsedFhmzbih
{
    public required IReadOnlyList<StationReading> Readings { get; init; }

    public required IReadOnlyList<SkippedStation> Skipped { get; init; }

    /// <summary>Oznake trenda koje nisu u rječniku. Prazno je očekivano stanje.</summary>
    public required IReadOnlyList<string> UnrecognisedTrends { get; init; }
}

/// <summary>
/// Čita dnevni hidrološki pregled FHMZBIH-a.
///
/// Klasičan server-rendered HTML, parsiran AngleSharpom (CLAUDE.md izričito zabranjuje regex
/// nad HTML-om). Tabela ima **spojene ćelije**: naziv vodotoka nosi `rowspan`, pa ga redovi
/// ispod nemaju — čita se s kraja reda, gdje je oblik stabilan.
/// </summary>
public static class FhmzbihParser
{
    /// <summary>
    /// Trend koji **agencija objavljuje**, kao ime slike u koloni Trend.
    ///
    /// `R` raste, `O` opada, `S` stagnira. `S2` se pojavljuje u podacima ali mu značenje nije
    /// dokumentovano — namjerno se **ne pogađa**, nego ostaje `Unknown` uz sačuvanu oznaku.
    /// Pogrešno pogođen smjer trenda je pogrešan podatak o rijeci.
    /// </summary>
    private static readonly Dictionary<string, TrendDirection> TrendCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["R"] = TrendDirection.Rising,
            ["O"] = TrendDirection.Falling,
            ["S"] = TrendDirection.Steady,
        };

    public static ParsedFhmzbih ParseIndex(
        string html,
        SourceClock clock,
        Attribution attribution,
        TimeSpan expectedInterval,
        TimeSpan publicationLag,
        IReadOnlyDictionary<string, FhmzbihStationDetails>? detailsByName = null)
    {
        var document = new HtmlParser().ParseDocument(html);
        var rows = document.QuerySelectorAll("table tr");

        var readings = new List<StationReading>();
        var skipped = new List<SkippedStation>();
        var unrecognised = new SortedSet<string>(StringComparer.Ordinal);
        var river = "";

        foreach (var row in rows)
        {
            var cells = row.QuerySelectorAll("td");

            // Red podataka ima 6 ćelija, ili 7 kad nosi i naziv vodotoka preko `rowspan`.
            if (cells.Length is not (6 or 7))
            {
                continue;
            }

            if (cells.Length == 7)
            {
                river = Text(cells[0]);
            }

            var count = cells.Length;
            var name = Text(cells[count - 6]);
            var date = Text(cells[count - 5]);
            var time = Text(cells[count - 4]);
            var value = Text(cells[count - 3]);
            var trendCode = TrendCode(cells[count - 2]);
            var threshold = Text(cells[count - 1]);

            if (name.Length == 0 || date.Length == 0)
            {
                continue;
            }

            try
            {
                readings.Add(ReadOne(
                    name, river, date, time, value, trendCode, threshold,
                    clock, attribution, expectedInterval, publicationLag,
                    detailsByName, unrecognised));
            }
            catch (Exception ex)
            {
                // Neuspjeh na jednoj stanici je normalno stanje koje se logira, ne pad runa
                // (SOURCES.md → kontrolna lista).
                skipped.Add(new SkippedStation(name, ex.Message));
            }
        }

        if (readings.Count == 0 && skipped.Count == 0)
        {
            throw new SourceResponseException(
                "U pregledu nema nijednog reda sa stanicom. Stranica je vjerovatno promijenjena "
                + "ili je vraćena greška umjesto tabele.");
        }

        return new ParsedFhmzbih
        {
            Readings = readings,
            Skipped = skipped,
            UnrecognisedTrends = [.. unrecognised],
        };
    }

    private static StationReading ReadOne(
        string name,
        string river,
        string date,
        string time,
        string value,
        string? trendCode,
        string threshold,
        SourceClock clock,
        Attribution attribution,
        TimeSpan expectedInterval,
        TimeSpan publicationLag,
        IReadOnlyDictionary<string, FhmzbihStationDetails>? detailsByName,
        SortedSet<string> unrecognised)
    {
        FhmzbihStationDetails? details = null;
        detailsByName?.TryGetValue(name, out details);

        var station = new Station
        {
            SourceId = FhmzbihSource.Id,
            StationKey = name,
            Name = name,
            River = details?.River ?? (river.Length > 0 ? river : null),
            Coordinates = details?.Coordinates,
            GaugeZero = details?.GaugeZero,
            ExpectedInterval = expectedInterval,
            TypicalPublicationLag = publicationLag,
            Attribution = attribution,
        };

        var thresholds = ParseDecimal(threshold) is { } limit
            ? new Thresholds
            {
                DefinedBy = attribution.AgencyName,
                // Ime praga je doslovno iz zaglavlja kolone. Stupanj se **ne** dodjeljuje:
                // agencija ne kaže da prelazak tog praga znači konkretan stepen opasnosti,
                // nego da se stanovništvo i CZ kontinuirano obavještavaju.
                Values = [new Threshold("Kontinuirano obavještavanje stanovništva i CZ", limit)],
            }
            : Thresholds.None(attribution.AgencyName);

        // FHMZBIH ne objavljuje stupanj opasnosti po stanici. Legenda sa tri stanja postoji
        // na stranici, ali redovi tabele ne nose nijednu od njenih ikona, a ni podstranice
        // stanica. Kao i kod AVPJM-a: imamo broj, nemamo tvrdnju o njemu.
        const string noStatusFromSource = "";

        var measuredAt = ParseLocalTimestamp(date, time, clock);
        var level = ParseDecimal(value);

        if (measuredAt is null && level is null)
        {
            return NoData(station, noStatusFromSource, "Red nema ni vodostaj ni vrijeme.", thresholds);
        }

        if (measuredAt is null)
        {
            return NoData(
                station, noStatusFromSource,
                $"Vrijeme `{date} {time}` se ne može pročitati.", thresholds);
        }

        if (level is null)
        {
            return NoData(station, noStatusFromSource, "Nema vrijednosti vodostaja.", thresholds);
        }

        PublishedTrend? trend = null;
        if (trendCode is { Length: > 0 })
        {
            if (!TrendCodes.TryGetValue(trendCode, out var direction))
            {
                direction = TrendDirection.Unknown;
                unrecognised.Add(trendCode);
            }

            trend = new PublishedTrend(trendCode, direction);
        }

        return new StationReading.Measured
        {
            Station = station,
            StatusLabelOriginal = noStatusFromSource,
            Thresholds = thresholds,
            ClaimedLevel = AlertLevel.Unknown,
            Trend = trend,
            MeasuredValue = new Measurement(level.Value, measuredAt.Value),
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

    /// <summary>
    /// Čita podstranicu stanice: koordinate, kotu nule, rijeku i sliv.
    ///
    /// Ovo se mijenja rijetko, pa se povlači jednom dnevno — dvanaest zahtjeva na dan.
    /// </summary>
    public static FhmzbihStationDetails? ParseStationPage(string html, string name)
    {
        var document = new HtmlParser().ParseDocument(html);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in document.QuerySelectorAll("table tr"))
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length < 2)
            {
                continue;
            }

            var key = Text(cells[0]);
            if (key.Length > 0)
            {
                values[key] = Text(cells[1]);
            }
        }

        if (values.Count == 0)
        {
            return null;
        }

        // Naziv ključa za kotu nule nije stabilan — na jednoj stranici je
        // `Kota "0" vodomjera (m n.m.)`, na drugoj `... (m n.m.9`. Zato se traži po početku.
        var gaugeZero = values
            .Where(pair => pair.Key.StartsWith("Kota", StringComparison.OrdinalIgnoreCase))
            .Select(pair => ParseDecimal(pair.Value))
            .FirstOrDefault(value => value is not null);

        return new FhmzbihStationDetails(
            name,
            ParseCoordinates(values.GetValueOrDefault("Koodrdinate stanice")
                             ?? values.GetValueOrDefault("Koordinate stanice")),
            gaugeZero,
            values.GetValueOrDefault("rijeka"),
            values.GetValueOrDefault("sliv"));
    }

    /// <summary>
    /// Koordinate su `lat lon`, ali **format nije isti na svim njihovim stranicama**:
    /// Bihać ima `44.81367 15.87508`, a Reljevo `43.88669N 18.31826E`.
    ///
    /// Zbog toga je šest od dvanaest stanica tri dana stajalo bez koordinata i nije se
    /// pojavljivalo na mapi. Slovo strane svijeta se skida; južna i zapadna hemisfera se
    /// poštuju iako u BiH ne dolaze — pravilo koje vrijedi samo za jedan slučaj je pravilo
    /// koje puca čim se granica pomjeri.
    /// </summary>
    private static Coordinates? ParseCoordinates(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(
            [' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
        {
            return null;
        }

        var latitude = ParseHemisphere(parts[0], 'S');
        var longitude = ParseHemisphere(parts[1], 'W');

        return latitude is not null && longitude is not null
            ? new Coordinates(latitude.Value, longitude.Value)
            : null;
    }

    private static double? ParseHemisphere(string text, char negative)
    {
        var trimmed = text.Trim();
        var sign = 1.0;

        if (trimmed.Length > 0 && char.IsLetter(trimmed[^1]))
        {
            if (char.ToUpperInvariant(trimmed[^1]) == char.ToUpperInvariant(negative))
            {
                sign = -1.0;
            }

            trimmed = trimmed[..^1].Trim();
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? sign * value
            : null;
    }

    /// <summary>
    /// `5.8.2026` + `08:00` u lokalnom vremenu, sa punim DST pravilima — za razliku od
    /// AVPJM-a, koji cijele godine ostaje na zimskom (SOURCES.md §3).
    /// </summary>
    private static DateTimeOffset? ParseLocalTimestamp(string date, string time, SourceClock clock)
    {
        var text = $"{date.Trim()} {time.Trim()}";

        string[] formats = ["d.M.yyyy HH:mm", "dd.MM.yyyy HH:mm", "d.M.yyyy H:mm"];

        if (!DateTime.TryParseExact(
                text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return null;
        }

        try
        {
            return clock.Resolve(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified));
        }
        catch (InvalidTimeZoneTimeException)
        {
            // Preskočeni sat na proljetnom prelazu. Vrijeme koje ne postoji se ne izmišlja.
            return null;
        }
    }

    private static decimal? ParseDecimal(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        decimal.TryParse(
            value.Replace(',', '.').Trim(),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    /// <summary>Trend je slika; oznaka je ime fajla bez ekstenzije.</summary>
    private static string? TrendCode(IElement cell)
    {
        var source = cell.QuerySelector("img")?.GetAttribute("src");
        if (source is null)
        {
            var text = Text(cell);
            return text.Length is > 0 and <= 3 ? text : null;
        }

        var file = source.Split('/').Last();
        var dot = file.LastIndexOf('.');
        return dot > 0 ? file[..dot] : file;
    }

    private static string Text(IElement element) =>
        element.TextContent.Replace(' ', ' ').Trim();
}
