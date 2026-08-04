using System.Globalization;
using System.Text;
using System.Text.Json;
using AngleSharp.Html.Parser;

namespace Vodostaji.Probe;

/// <summary>
/// Jedno očitanje, onako kako ga je izvor dao — bez ijedne korekcije.
/// <paramref name="MeasuredAtRaw"/> je doslovna vrijednost iz izvora, a
/// <paramref name="MeasuredAtNaiveUtc"/> je ista vrijednost pročitana kao da je UTC.
/// Razlika između te dvije kolone i <paramref name="ObservedAtUtc"/> je cijela poenta.
/// </summary>
internal sealed record WatchRow(
    DateTimeOffset ObservedAtUtc,
    string SourceId,
    string Key,
    string Label,
    string? Value,
    string? MeasuredAtRaw,
    DateTimeOffset? MeasuredAtNaiveUtc,
    string? Status);

/// <summary>
/// Ponavljano očitavanje malog broja endpointa, radi jedne stvari: da se izmjeri
/// pomak između vremena koje izvor tvrdi i stvarnog vremena.
///
/// Ovo NIJE adapter i ne smije postati adapter. Izvlači samo ono što ide u CSV,
/// namjerno bez modela, mapiranja statusa i validacije — sve to pripada Fazi 1.
/// </summary>
internal sealed class Watcher(ProbeClient client, string fixtureRoot)
{
    private const string SavaUrl =
        "https://isvportal.voda.ba/server/rest/services/Hidrolosko_stane_u_realnom_vremenu/" +
        "FeatureServer/0/query?where=1%3D1&outFields=SEC_ID,description,H_CM,DATE_TIME,CURRENT_STATUS" +
        "&returnGeometry=false&f=json";

    private const string FhmzUrl = "https://www.fhmzbih.gov.ba/latinica/HIDRO/";
    private const string AvpjmUrl = "https://avpjm.jadran.ba/vodomjerne_stanice/1";

    private static readonly DateTimeOffset UnixEpoch = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// `DATE_TIME` je `null` na dionicama bez podatka — 11 od 45 u snimku od 2026-08-04.
    /// Provjera vrste je obavezna: `TryGetInt64` nad `Null` baca, ne vraća false.
    /// </summary>
    private static long? Epoch(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var ms)
            ? ms
            : null;

    public async Task<int> RunCycleAsync(DateTimeOffset now, CancellationToken ct)
    {
        var rows = new List<WatchRow>();

        rows.AddRange(await SavaAsync(now, ct).ConfigureAwait(false));
        rows.AddRange(await FhmzbihAsync(now, ct).ConfigureAwait(false));
        rows.AddRange(await AvpjmAsync(now, ct).ConfigureAwait(false));

        await AppendAsync(rows, ct).ConfigureAwait(false);
        return rows.Count;
    }

    private async Task<IEnumerable<WatchRow>> SavaAsync(DateTimeOffset now, CancellationToken ct)
    {
        var rows = new List<WatchRow>();
        var body = await client.FetchAsync("_watch","sava-dionice", SavaUrl, "json", ct).ConfigureAwait(false);
        if (body is null)
        {
            return rows;
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("features", out var features))
        {
            return rows;
        }

        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("attributes", out var a))
            {
                continue;
            }

            var ms = Epoch(a, "DATE_TIME");

            rows.Add(new WatchRow(
                now,
                "avp-sava",
                Raw(a, "SEC_ID"),
                Raw(a, "description"),
                Raw(a, "H_CM"),
                ms?.ToString(CultureInfo.InvariantCulture),
                ms is null ? null : UnixEpoch.AddMilliseconds(ms.Value),
                Raw(a, "CURRENT_STATUS")));
        }

        return rows;
    }

    private async Task<IEnumerable<WatchRow>> FhmzbihAsync(DateTimeOffset now, CancellationToken ct)
    {
        var rows = new List<WatchRow>();
        var body = await client.FetchAsync("_watch","fhmzbih-hidro", FhmzUrl, "html", ct).ConfigureAwait(false);
        if (body is null)
        {
            return rows;
        }

        var document = await new HtmlParser().ParseDocumentAsync(body, ct).ConfigureAwait(false);
        var vodotok = "";

        foreach (var tr in document.QuerySelectorAll("table tr"))
        {
            var cells = tr.QuerySelectorAll("td")
                .Select(td => td.TextContent.Replace('\n', ' ').Trim())
                .ToList();

            // Vodotok se u tabeli spaja preko više redova, pa ga uži red nema — nasljeđuje se.
            // Čita se s kraja jer je rep reda stabilan, a glava nije.
            if (cells.Count is < 6 or > 7)
            {
                continue;
            }

            if (cells.Count == 7)
            {
                vodotok = cells[0];
            }

            var n = cells.Count;
            var station = cells[n - 6];
            var date = cells[n - 5];
            var time = cells[n - 4];
            var level = cells[n - 3];

            if (station.Length == 0 || date.Length == 0)
            {
                continue;
            }

            var raw = $"{date} {time}";
            DateTimeOffset? naive = DateTime.TryParseExact(
                raw, ["d.M.yyyy HH:mm", "dd.MM.yyyy HH:mm"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? new DateTimeOffset(parsed, TimeSpan.Zero)
                : null;

            rows.Add(new WatchRow(
                now, "fhmzbih", station, vodotok, level, raw, naive, null));
        }

        return rows;
    }

    private async Task<IEnumerable<WatchRow>> AvpjmAsync(DateTimeOffset now, CancellationToken ct)
    {
        var rows = new List<WatchRow>();
        var body = await client.FetchAsync("_watch","avpjm-mostar", AvpjmUrl, "html", ct).ConfigureAwait(false);
        if (body is null)
        {
            return rows;
        }

        var document = await new HtmlParser().ParseDocumentAsync(body, ct).ConfigureAwait(false);
        var prop = document.QuerySelector("station-map")?.GetAttribute(":station");
        if (prop is null)
        {
            return rows;
        }

        try
        {
            using var doc = JsonDocument.Parse(prop);
            var station = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement[0]
                : doc.RootElement;

            var seconds = Epoch(station, "valtime");

            rows.Add(new WatchRow(
                now,
                "avpjm",
                Raw(station, "id"),
                Raw(station, "title"),
                Raw(station, "val"),
                seconds?.ToString(CultureInfo.InvariantCulture),
                seconds is null ? null : UnixEpoch.AddSeconds(seconds.Value),
                Raw(station, "status")));
        }
        catch (JsonException)
        {
            // Promijenjen oblik propa je sam po sebi nalaz — ciklus se ne ruši zbog njega.
        }

        return rows;
    }

    private async Task AppendAsync(IReadOnlyList<WatchRow> rows, CancellationToken ct)
    {
        var dir = Path.Combine(fixtureRoot, "_watch");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "watch.csv");

        var sb = new StringBuilder();
        if (!File.Exists(path))
        {
            sb.AppendLine("observed_at_utc,source,key,label,value,measured_at_raw,measured_at_naive_utc,lag_minutes,status");
        }

        foreach (var r in rows)
        {
            var lag = r.MeasuredAtNaiveUtc is null
                ? ""
                : (r.ObservedAtUtc - r.MeasuredAtNaiveUtc.Value).TotalMinutes.ToString("F0", CultureInfo.InvariantCulture);

            sb.Append(r.ObservedAtUtc.ToString("O", CultureInfo.InvariantCulture)).Append(',')
              .Append(Csv(r.SourceId)).Append(',')
              .Append(Csv(r.Key)).Append(',')
              .Append(Csv(r.Label)).Append(',')
              .Append(Csv(r.Value)).Append(',')
              .Append(Csv(r.MeasuredAtRaw)).Append(',')
              .Append(r.MeasuredAtNaiveUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(lag).Append(',')
              .Append(Csv(r.Status)).AppendLine();
        }

        await File.AppendAllTextAsync(path, sb.ToString(), ct).ConfigureAwait(false);
    }

    private static string Raw(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.Null => "",
                _ => value.GetRawText(),
            }
            : "";

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }
}
