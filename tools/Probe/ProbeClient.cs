using System.Diagnostics;
using System.Net;

using Vodostaji.Core;

namespace Vodostaji.Probe;

internal sealed record ProbeResult(
    string SourceId,
    string Name,
    string Url,
    HttpStatusCode? StatusCode,
    string? ContentType,
    long Bytes,
    TimeSpan Elapsed,
    string? SavedPath,
    string? Error)
{
    public bool Ok => Error is null && StatusCode == HttpStatusCode.OK;
}

/// <summary>
/// Povlači i snima sirove odgovore. Ne interpretira ništa — interpretacija je posao
/// <see cref="SchemaReport"/>, i radi se nad snimljenim fajlom, ne nad mrežnim odgovorom.
/// Tako je izvještaj reproducibilan bez pristupa mreži.
/// </summary>
internal sealed class ProbeClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _fixtureRoot;
    private readonly TimeSpan _delay;
    private readonly string _stamp;
    private readonly List<ProbeResult> _results = [];

    public ProbeClient(string fixtureRoot, TimeSpan delay, DateTimeOffset runStamp)
    {
        _fixtureRoot = fixtureRoot;
        _delay = delay;
        _stamp = runStamp.ToString("yyyy-MM-dd");

        _http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(Contact.ProbeUserAgent);
    }

    public IReadOnlyList<ProbeResult> Results => _results;

    /// <summary>
    /// Snima u tests/fixtures/&lt;source&gt;/&lt;name&gt;-YYYY-MM-DD.&lt;ext&gt;
    /// Vraća sadržaj odgovora, ili null ako zahtjev nije uspio.
    /// </summary>
    public async Task<string?> FetchAsync(
        string sourceId,
        string name,
        string url,
        string extension,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            sw.Stop();

            var error = response.IsSuccessStatusCode
                ? ArcGisError(body)
                : $"HTTP {(int)response.StatusCode}";

            // Neuspjeh se snima kao dokaz, ali izvan fixtures foldera — fixture mora biti
            // upotrebljiv kao ulaz u test, a poruka o grešci to nije.
            var saved = response.IsSuccessStatusCode
                ? await SaveAsync(sourceId, name, extension, body, error is not null, ct).ConfigureAwait(false)
                : null;

            var result = new ProbeResult(
                sourceId, name, url,
                response.StatusCode,
                response.Content.Headers.ContentType?.MediaType,
                body.Length,
                sw.Elapsed,
                saved,
                error);

            _results.Add(result);
            Log(result);

            await Task.Delay(_delay, ct).ConfigureAwait(false);
            return error is null ? body : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var result = new ProbeResult(sourceId, name, url, null, null, 0, sw.Elapsed, null, ex.Message);
            _results.Add(result);
            Log(result);
            return null;
        }
    }

    /// <summary>
    /// ArcGIS vraća greške sa statusom 200 i omotačem <c>{"error":{...}}</c> u tijelu.
    /// Ko gleda samo HTTP status, vidi uspjeh tamo gdje ga nema — isti obrazac kao
    /// `Unknown` koji postane `Normal`. Zato se tijelo uvijek pregleda.
    /// </summary>
    private static string? ArcGisError(string body)
    {
        if (!body.TrimStart().StartsWith('{'))
        {
            return null;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("error", out var error))
            {
                return null;
            }

            var code = error.TryGetProperty("code", out var c) ? c.GetRawText() : "?";
            var message = error.TryGetProperty("message", out var m) ? m.GetString() : "nepoznata greška";
            return $"ArcGIS {code}: {message}";
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private async Task<string> SaveAsync(
        string sourceId, string name, string extension, string body, bool isError, CancellationToken ct)
    {
        var dir = isError
            ? Path.Combine(_fixtureRoot, sourceId, "_errors")
            : Path.Combine(_fixtureRoot, sourceId);
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"{Sanitize(name)}-{_stamp}.{extension}");
        await File.WriteAllTextAsync(path, body, ct).ConfigureAwait(false);
        return path;
    }

    /// <summary>Imena slojeva nose '/' i dijakritiku — spljošti u nešto što preživi svaki fajlsistem.</summary>
    private static string Sanitize(string name)
    {
        var chars = name.Select(c => c switch
        {
            '/' or '\\' or ' ' => '-',
            _ when Path.GetInvalidFileNameChars().Contains(c) => '-',
            _ => c,
        }).ToArray();

        return new string(chars).Trim('-');
    }

    private static void Log(ProbeResult r)
    {
        var status = r.Ok ? "  ok" : "FAIL";
        var colour = r.Ok ? ConsoleColor.Green : ConsoleColor.Red;

        Console.ForegroundColor = colour;
        Console.Write($"[{status}] ");
        Console.ResetColor();

        Console.Write($"{r.SourceId}/{r.Name}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  {r.Bytes:N0}b  {r.Elapsed.TotalMilliseconds:N0}ms");
        if (r.Error is not null)
        {
            Console.Write($"  — {r.Error}");
        }
        Console.ResetColor();
        Console.WriteLine();
    }

    public void Dispose() => _http.Dispose();
}
