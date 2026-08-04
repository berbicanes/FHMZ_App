using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Vodostaji.Probe;

/// <summary>
/// Čita snimljene fixtures i pravi markdown izvještaj iz kojeg se regeneriše SOURCES.md.
/// Radi nad fajlovima, ne nad mrežom — izvještaj se može ponoviti bez ijednog novog zahtjeva.
/// </summary>
internal static class SchemaReport
{
    public static async Task<string> WriteAsync(
        string fixtureRoot,
        DateTimeOffset runStamp,
        IReadOnlyList<ProbeResult> results,
        IReadOnlyList<DiscoveredService> discovered,
        CancellationToken ct)
    {
        var stamp = runStamp.ToString("yyyy-MM-dd");
        var sb = new StringBuilder();

        sb.AppendLine($"# Probe izvještaj — {stamp}");
        sb.AppendLine();
        sb.AppendLine($"Generisano: `{runStamp:yyyy-MM-dd HH:mm:ss}Z` · User-Agent: `{Contact.UserAgent}`");
        sb.AppendLine();
        sb.AppendLine("Ovaj fajl je izvor istine za regeneraciju `docs/SOURCES.md`.");
        sb.AppendLine();

        AppendRequestSummary(sb, results);
        AppendCatalog(sb, discovered);
        await AppendLayerSchemasAsync(sb, results, ct).ConfigureAwait(false);

        var dir = Path.Combine(fixtureRoot, "_report");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"schema-{stamp}.md");
        await File.WriteAllTextAsync(path, sb.ToString(), ct).ConfigureAwait(false);
        return path;
    }

    private static void AppendRequestSummary(StringBuilder sb, IReadOnlyList<ProbeResult> results)
    {
        var ok = results.Count(r => r.Ok);

        sb.AppendLine("## Zahtjevi");
        sb.AppendLine();
        sb.AppendLine($"{ok} od {results.Count} uspješno.");
        sb.AppendLine();
        sb.AppendLine("| Izvor | Naziv | Status | Bajta | Fixture |");
        sb.AppendLine("|---|---|---|---|---|");

        foreach (var r in results)
        {
            var status = r.Error ?? ((int?)r.StatusCode)?.ToString(CultureInfo.InvariantCulture) ?? "—";
            var fixture = r.SavedPath is null ? "—" : $"`{Path.GetFileName(r.SavedPath)}`";
            sb.AppendLine($"| {r.SourceId} | {r.Name} | {status} | {r.Bytes:N0} | {fixture} |");
        }

        sb.AppendLine();
    }

    private static void AppendCatalog(StringBuilder sb, IReadOnlyList<DiscoveredService> discovered)
    {
        if (discovered.Count == 0)
        {
            return;
        }

        sb.AppendLine("## Otkriveni ArcGIS servisi");
        sb.AppendLine();
        sb.AppendLine("Ovo je stvarni sadržaj kataloga, ne prepis iz dokumentacije.");
        sb.AppendLine();
        sb.AppendLine("| Servis | Tip | U SOURCES.md |");
        sb.AppendLine("|---|---|---|");

        foreach (var s in discovered.OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            var known = ProbeTargets.ServicesOfInterest.Contains(s.ShortName) ? "da" : "—";
            sb.AppendLine($"| `{s.Name}` | {s.Type} | {known} |");
        }

        sb.AppendLine();

        var missing = ProbeTargets.ServicesOfInterest
            .Where(name => !discovered.Any(d => string.Equals(d.ShortName, name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missing.Count > 0)
        {
            sb.AppendLine("**Navedeni u SOURCES.md ali nisu nađeni u katalogu:**");
            sb.AppendLine();
            foreach (var name in missing)
            {
                sb.AppendLine($"- `{name}`");
            }
            sb.AppendLine();
        }
    }

    private static async Task AppendLayerSchemasAsync(
        StringBuilder sb, IReadOnlyList<ProbeResult> results, CancellationToken ct)
    {
        sb.AppendLine("## Sheme slojeva");
        sb.AppendLine();

        foreach (var r in results.Where(r => r.Ok && r.SavedPath is not null && !r.Name.EndsWith("-sample", StringComparison.Ordinal)))
        {
            var json = await File.ReadAllTextAsync(r.SavedPath!, ct).ConfigureAwait(false);

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                continue;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("fields", out var fields))
                {
                    continue;
                }

                sb.AppendLine($"### `{r.Name}`");
                sb.AppendLine();

                AppendLayerHeader(sb, root, r.Url);
                AppendFields(sb, fields);
                AppendRenderer(sb, root);
            }
        }
    }

    private static void AppendLayerHeader(StringBuilder sb, JsonElement root, string url)
    {
        var name = ReadString(root, "name") ?? "—";
        var type = ReadString(root, "type") ?? "—";
        var geometry = ReadString(root, "geometryType") ?? "—";
        var idField = ReadString(root, "objectIdField") ?? "—";
        var displayField = ReadString(root, "displayField") ?? "—";

        sb.AppendLine($"- **Naziv:** {name}");
        sb.AppendLine($"- **Tip:** {type} · **Geometrija:** {geometry}");
        sb.AppendLine($"- **ObjectId:** `{idField}` · **Display:** `{displayField}`");
        sb.AppendLine($"- **URL:** `{url}`");

        if (root.TryGetProperty("maxRecordCount", out var max) && max.TryGetInt32(out var maxValue))
        {
            sb.AppendLine($"- **MaxRecordCount:** {maxValue}");
        }

        sb.AppendLine();
    }

    private static void AppendFields(StringBuilder sb, JsonElement fields)
    {
        if (fields.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        sb.AppendLine("| Polje | Tip | Alias | Domen |");
        sb.AppendLine("|---|---|---|---|");

        foreach (var field in fields.EnumerateArray())
        {
            var name = ReadString(field, "name") ?? "—";
            var type = (ReadString(field, "type") ?? "—").Replace("esriFieldType", "", StringComparison.Ordinal);
            var alias = ReadString(field, "alias") ?? "";
            sb.AppendLine($"| `{name}` | {type} | {alias} | {DescribeDomain(field)} |");
        }

        sb.AppendLine();
    }

    private static string DescribeDomain(JsonElement field)
    {
        if (!field.TryGetProperty("domain", out var domain) || domain.ValueKind != JsonValueKind.Object)
        {
            return "—";
        }

        if (!domain.TryGetProperty("codedValues", out var coded) || coded.ValueKind != JsonValueKind.Array)
        {
            return ReadString(domain, "type") ?? "—";
        }

        var values = coded.EnumerateArray()
            .Select(v => $"{RawValue(v, "code")}={ReadString(v, "name")}")
            .ToList();

        return values.Count == 0 ? "—" : string.Join("<br>", values);
    }

    private static void AppendRenderer(StringBuilder sb, JsonElement root)
    {
        if (!root.TryGetProperty("drawingInfo", out var drawing) ||
            !drawing.TryGetProperty("renderer", out var renderer))
        {
            return;
        }

        var type = ReadString(renderer, "type") ?? "—";
        sb.AppendLine($"**Renderer:** `{type}`");
        sb.AppendLine();

        if (!renderer.TryGetProperty("uniqueValueInfos", out var infos) || infos.ValueKind != JsonValueKind.Array)
        {
            if (renderer.TryGetProperty("symbol", out var symbol) && ToHex(symbol) is { } hex)
            {
                sb.AppendLine($"Jedinstven simbol: `{hex}`");
                sb.AppendLine();
            }
            return;
        }

        var field = ReadString(renderer, "field1") ?? "—";
        sb.AppendLine($"Polje: `{field}`");
        sb.AppendLine();
        sb.AppendLine("| Vrijednost | Label | Hex |");
        sb.AppendLine("|---|---|---|");

        foreach (var info in infos.EnumerateArray())
        {
            var value = RawValue(info, "value");
            var label = ReadString(info, "label") ?? "";
            var hex = info.TryGetProperty("symbol", out var symbol) ? ToHex(symbol) ?? "—" : "—";
            sb.AppendLine($"| `{value}` | {label} | `{hex}` |");
        }

        sb.AppendLine();
    }

    /// <summary>ArcGIS boju daje kao [r,g,b,a]; alfa se ne prenosi u hex jer je UI ne koristi.</summary>
    private static string? ToHex(JsonElement symbol)
    {
        if (!symbol.TryGetProperty("color", out var color) || color.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var parts = color.EnumerateArray()
            .Where(v => v.TryGetInt32(out _))
            .Select(v => v.GetInt32())
            .ToArray();

        return parts.Length < 3
            ? null
            : $"#{parts[0]:X2}{parts[1]:X2}{parts[2]:X2}";
    }

    private static string RawValue(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.Null => "null",
                _ => value.GetRawText(),
            }
            : "—";

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
