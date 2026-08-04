using System.Text.Json;

namespace Vodostaji.Probe;

internal sealed record DiscoveredService(string Name, string Type)
{
    /// <summary>Katalog vraća imena sa prefiksom foldera — poređenje ide po zadnjem segmentu.</summary>
    public string ShortName => Name.Split('/').Last();

    public string Path => $"{Name}/{Type}";
}

/// <summary>
/// Otkriva ArcGIS katalog umjesto da ga pogađa. SOURCES.md je neverifikovan po vlastitom
/// priznanju, pa se lista servisa čita sa servera, a dokument služi samo da kaže koji su
/// od otkrivenih dovoljno bitni da se buši do nivoa polja.
/// </summary>
internal sealed class ArcGisCrawler(ProbeClient client, string sourceId, string root)
{
    private const int MaxLayersPerService = 200;

    private readonly List<DiscoveredService> _discovered = [];

    public IReadOnlyList<DiscoveredService> Discovered => _discovered;

    public async Task CrawlAsync(bool drillAll, bool sampleAll, CancellationToken ct)
    {
        await DiscoverCatalogAsync(ct).ConfigureAwait(false);

        foreach (var service in _discovered)
        {
            if (!drillAll && !ProbeTargets.ServicesOfInterest.Contains(service.ShortName))
            {
                continue;
            }

            await DrillServiceAsync(service, sampleAll, ct).ConfigureAwait(false);
        }
    }

    private async Task DiscoverCatalogAsync(CancellationToken ct)
    {
        var body = await client.FetchAsync(sourceId, "catalog-root", $"{root}?f=json", "json", ct)
            .ConfigureAwait(false);
        if (body is null)
        {
            return;
        }

        using var doc = JsonDocument.Parse(body);
        CollectServices(doc.RootElement);

        foreach (var folder in ReadStringArray(doc.RootElement, "folders"))
        {
            var folderBody = await client
                .FetchAsync(sourceId, $"catalog-folder-{folder}", $"{root}/{Escape(folder)}?f=json", "json", ct)
                .ConfigureAwait(false);
            if (folderBody is null)
            {
                continue;
            }

            using var folderDoc = JsonDocument.Parse(folderBody);
            CollectServices(folderDoc.RootElement);
        }
    }

    private void CollectServices(JsonElement element)
    {
        if (!element.TryGetProperty("services", out var services) ||
            services.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var service in services.EnumerateArray())
        {
            var name = ReadString(service, "name");
            var type = ReadString(service, "type");
            if (name is null || type is null)
            {
                continue;
            }

            if (!_discovered.Any(d => d.Name == name && d.Type == type))
            {
                _discovered.Add(new DiscoveredService(name, type));
            }
        }
    }

    private async Task DrillServiceAsync(DiscoveredService service, bool sampleAll, CancellationToken ct)
    {
        var serviceUrl = $"{root}/{EscapePath(service.Name)}/{service.Type}";
        var label = $"{service.ShortName}-{service.Type}";

        var body = await client.FetchAsync(sourceId, label, $"{serviceUrl}?f=json", "json", ct)
            .ConfigureAwait(false);
        if (body is null)
        {
            return;
        }

        using var doc = JsonDocument.Parse(body);

        var layers = ReadLayerIds(doc.RootElement, "layers")
            .Concat(ReadLayerIds(doc.RootElement, "tables"))
            .Distinct()
            .Take(MaxLayersPerService)
            .ToList();

        foreach (var id in layers)
        {
            var layerBody = await client
                .FetchAsync(sourceId, $"{label}-{id}", $"{serviceUrl}/{id}?f=json", "json", ct)
                .ConfigureAwait(false);
            if (layerBody is null)
            {
                continue;
            }

            if (sampleAll || ProbeTargets.WantsSample(service.ShortName, id))
            {
                await SampleAsync(serviceUrl, label, id, layerBody, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Uzorak, ne dump. Puna baza svih stanica u repou je tačno onaj scenarij koji
    /// LEGAL.md §1 opisuje kao rizičan — za testove je dovoljno 25 zapisa uz punu shemu.
    /// </summary>
    private async Task SampleAsync(
        string serviceUrl, string label, int id, string layerMetadata, CancellationToken ct)
    {
        var hasGeometry = false;
        var supportsGeoJson = false;
        try
        {
            using var doc = JsonDocument.Parse(layerMetadata);
            hasGeometry = ReadString(doc.RootElement, "geometryType") is { Length: > 0 };

            // MapServer slojevi prijavljuju geometriju ali često ne serviraju geoJSON —
            // pitaj sloj šta podržava umjesto da zaključuješ iz tipa servisa.
            var formats = ReadString(doc.RootElement, "supportedQueryFormats") ?? "";
            supportsGeoJson = formats.Contains("geoJSON", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            // Nečitljiva metadata nije razlog da preskočimo uzorak — pokušaj kao tabelu.
        }

        var format = hasGeometry && supportsGeoJson ? "geojson" : "json";
        var geometry = hasGeometry.ToString().ToLowerInvariant();
        var name = $"{label}-{id}-sample";

        var baseQuery = $"{serviceUrl}/{id}/query?where=1%3D1&outSR=4326" +
                        $"&resultRecordCount=25&returnGeometry={geometry}&f={format}";

        // Prvi pokušaj nosi svoje ime da se u izvještaju vidi razlika između sonde koja je
        // odbijena i uzorka koji stvarno nedostaje. Jedno je saznanje, drugo je rupa.
        var body = await client
            .FetchAsync(sourceId, $"{name}-outfields-all", $"{baseQuery}&outFields=*", "json", ct)
            .ConfigureAwait(false);
        if (body is not null)
        {
            return;
        }

        // Neki slojevi prijavljuju polja koja ne mogu servirati — na `ISV_BIH_2009_javnakarta/1`
        // to je OBJECTID uz `objectIdField: null`, i on sam ruši `outFields=*`. Ime po ime prolazi.
        var explicitFields = SafeFieldNames(layerMetadata);
        if (explicitFields.Count == 0)
        {
            return;
        }

        Console.WriteLine($"       ↳ outFields=* odbijen, pokušavam sa {explicitFields.Count} imenovanih polja");

        var encoded = string.Join(',', explicitFields.Select(Uri.EscapeDataString));
        await client.FetchAsync(sourceId, name, $"{baseQuery}&outFields={encoded}", "json", ct)
            .ConfigureAwait(false);
    }

    /// <summary>Polja koja se smiju tražiti poimence: bez geometrije i bez OID-a kad ga sloj nema.</summary>
    private static List<string> SafeFieldNames(string layerMetadata)
    {
        var names = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(layerMetadata);
            var root = doc.RootElement;
            var hasObjectId = ReadString(root, "objectIdField") is { Length: > 0 };

            if (!root.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Array)
            {
                return names;
            }

            foreach (var field in fields.EnumerateArray())
            {
                var name = ReadString(field, "name");
                var type = ReadString(field, "type") ?? "";

                if (name is null ||
                    type.Contains("Geometry", StringComparison.Ordinal) ||
                    (!hasObjectId && type.Contains("OID", StringComparison.Ordinal)))
                {
                    continue;
                }

                names.Add(name);
            }
        }
        catch (JsonException)
        {
            names.Clear();
        }

        return names;
    }

    private static IEnumerable<int> ReadLayerIds(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id) && id.TryGetInt32(out var value))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<string> ReadStringArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } value)
            {
                yield return value;
            }
        }
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Escape(string segment) => Uri.EscapeDataString(segment);

    /// <summary>Imena servisa nose dijakritiku i mogu nositi folder prefiks — svaki segment posebno.</summary>
    private static string EscapePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
