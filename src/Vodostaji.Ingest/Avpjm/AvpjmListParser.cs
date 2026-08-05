using System.Globalization;
using System.Text.Json;
using AngleSharp.Html.Parser;
using Vodostaji.Core;
using Vodostaji.Ingest;

namespace Vodostaji.Ingest.Avpjm;

/// <summary>
/// Čita listu stanica sa `https://avpjm.jadran.ba/vodomjerne_stanice`.
///
/// Stranica je server-rendered Laravel/Vue, a cijeli registar putuje kao HTML-escapovan JSON
/// u Vue propu `<stations-grid :items="…">`. Jedan zahtjev daje cijeli sliv (SOURCES.md §2).
///
/// Atribut se dohvata preko AngleSharpa, ne regexom — CLAUDE.md to izričito traži, a vrijednost
/// je i sama JSON string unutar HTML atributa, pa se mora odmotati u dva koraka.
/// </summary>
public static class AvpjmListParser
{
    public static ParsedAvpjm Parse(
        string html,
        SourceClock clock,
        Attribution attribution,
        TimeSpan expectedInterval,
        TimeSpan publicationLag = default)
    {
        var document = new HtmlParser().ParseDocument(html);

        var prop = document.QuerySelector("stations-grid")?.GetAttribute(":items")
                   ?? document.QuerySelector("stations-map")?.GetAttribute(":stations");

        if (prop is null)
        {
            throw new SourceResponseException(
                "Stranica nema `<stations-grid :items>` ni `<stations-map :stations>`. "
                + "Ili je stranica promijenjena, ili je odgovor greška umjesto liste.");
        }

        var readings = new List<StationReading>();
        var skipped = new List<SkippedStation>();

        using var document2 = JsonDocument.Parse(prop);

        if (document2.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new SourceResponseException("`:items` nije niz.");
        }

        foreach (var element in document2.RootElement.EnumerateArray())
        {
            var key = Key(element);
            if (key is null)
            {
                skipped.Add(new SkippedStation("?", "Zapis nema `id`."));
                continue;
            }

            try
            {
                readings.Add(ReadOne(element, key, clock, attribution, expectedInterval, publicationLag));
            }
            catch (Exception ex) when (ex is not SourceResponseException)
            {
                skipped.Add(new SkippedStation(key, ex.Message));
            }
        }

        return new ParsedAvpjm { Readings = readings, Skipped = skipped };
    }

    private static StationReading ReadOne(
        JsonElement element,
        string key,
        SourceClock clock,
        Attribution attribution,
        TimeSpan expectedInterval,
        TimeSpan publicationLag)
    {
        var station = BuildStation(element, key, attribution, expectedInterval, publicationLag);
        var thresholds = BuildThresholds(element, attribution.AgencyName);

        var value = Decimal(element, "val");
        var seconds = Epoch(element, "valtime");

        // AVPJM ne objavljuje stupanj opasnosti javnosti (SOURCES.md §2.1), pa doslovnog
        // teksta statusa nema. Prazan string je ovdje istina — nije da ga nismo pročitali,
        // nego ga nema.
        const string noStatusFromSource = "";

        if (value is null && seconds is null)
        {
            return NoData(station, noStatusFromSource, "Nema ni `val` ni `valtime`.", thresholds);
        }

        if (seconds is null)
        {
            return NoData(station, noStatusFromSource, "`valtime` nedostaje — vrijednost bez vremena mjerenja.", thresholds);
        }

        if (value is null)
        {
            return NoData(station, noStatusFromSource, "`val` nedostaje.", thresholds);
        }

        return new StationReading.Measured
        {
            Station = station,
            StatusLabelOriginal = noStatusFromSource,
            Thresholds = thresholds,

            // **Uvijek `Unknown`.** Imamo broj, nemamo tvrdnju o opasnosti. Njihov klijent
            // izvodi boju iz pragova, ali samo za prijavljenu ulogu `fop` — javnosti vraća
            // crno prije nego išta uporedi. Repliciranje bi značilo pokazati ocjenu koju
            // agencija namjerno ne pokazuje, po pravilu koje ni sami ne razumijemo.
            ClaimedLevel = AlertLevel.Unknown,

            MeasuredValue = new Measurement(value.Value, clock.ResolveEpochSeconds(seconds.Value)),
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
        JsonElement element,
        string key,
        Attribution attribution,
        TimeSpan expectedInterval,
        TimeSpan publicationLag) => new()
    {
        SourceId = AvpjmSource.Id,
        StationKey = key,
        Name = String(element, "title") ?? key,
        River = String(element, "vodotok"),
        Coordinates = ParseLocation(String(element, "location")),
        // `kota` je jedino polje koje stiže kao double sa punom round-trip ekspanzijom.
        // Zaokružuje se **samo ono**, i to eksplicitno na pozivnom mjestu — da se ne desi
        // da generički helper jednog dana tiho zaokruži i vodostaj.
        GaugeZero = RoundedDecimal(element, "kota", 6),
        ExpectedInterval = expectedInterval,
        TypicalPublicationLag = publicationLag,
        Attribution = attribution,
    };

    /// <summary>
    /// `location` je string oblika `"43.34835,17.8105"` — **lat pa lon**, obrnuto od GeoJSON-a.
    /// Zamjena bi stanice sa Neretve prebacila u Irak, pa se redoslijed ovdje ne pogađa.
    /// </summary>
    private static Coordinates? ParseLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var parts = location.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
        {
            return null;
        }

        return new Coordinates(latitude, longitude);
    }

    /// <summary>
    /// Pragovi stižu kao stringovi (`"850"`), kao `null`, i kao **prazan string**.
    /// Prazan prag je nepoznat prag, ne nula.
    /// </summary>
    private static Thresholds BuildThresholds(JsonElement element, string definedBy)
    {
        var values = new List<Threshold>();

        foreach (var field in new[] { "redovna_obrana", "vanredna_obrana", "kontinuirana_obrana" })
        {
            if (Decimal(element, field) is { } value)
            {
                // Stupanj se namjerno **ne** dodjeljuje. Redoslijed pragova kod AVPJM-a nije
                // skala ozbiljnosti — u njihovom kodu `kontinuirana` gazi `vanredna` gazi
                // `redovna`. Ime praga se prikazuje, značenje ostaje njihovo.
                values.Add(new Threshold(field, value));
            }
        }

        return new Thresholds { Values = values, DefinedBy = definedBy };
    }

    private static string? Key(JsonElement element) =>
        element.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var id)
            ? id.ToString(CultureInfo.InvariantCulture)
            : null;

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? Epoch(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var epoch)
            ? epoch
            : null;

    /// <summary>
    /// Zaokruženo čitanje, za polja koja stižu kao double serijalizovan sa punom preciznošću.
    ///
    /// `kota` dolazi kao `40.28999999999999914734871708787977695465087890625` — 47 decimala.
    /// `decimal` to **primi** i vrati `40.289999999999999147348717088`, što izgleda kao
    /// preciznost a jeste artefakt zapisa doublea. Namjeravana vrijednost je `40.29`.
    ///
    /// Nikad se ne koristi za vodostaj: mjerenje se prenosi doslovno, kako god ružno izgledalo.
    /// </summary>
    private static decimal? RoundedDecimal(JsonElement element, string name, int decimals) =>
        Decimal(element, name) is { } value ? Math.Round(value, decimals) : null;

    /// <summary>
    /// Broj koji može stići kao broj ili kao string. Vrijednost se prenosi doslovno —
    /// zaokruživanje ide isključivo kroz <see cref="RoundedDecimal"/>, na izričit poziv.
    /// </summary>
    private static decimal? Decimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.TryGetDecimal(out var number) ? number : null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text)
                ? null
                : decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : null;
        }

        return null;
    }
}

public sealed record ParsedAvpjm
{
    public required IReadOnlyList<StationReading> Readings { get; init; }

    public required IReadOnlyList<SkippedStation> Skipped { get; init; }
}
