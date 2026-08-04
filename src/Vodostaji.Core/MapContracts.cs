using System.Text.Json.Serialization;

namespace Vodostaji.Core;

/// <summary>
/// Svojstva jedne dionice u GeoJSON-u koji mapa čita.
///
/// Postoji kao tip, a ne kao ručno sklopljen JSON objekat, da bi bio **jedan izvor istine**:
/// backend serijalizuje ovo, OpenAPI ga dokumentuje, a TypeScript tipovi se iz njega
/// generišu (CLAUDE.md → Konvencije). Ručno pisan TS interfejs bi se prije ili kasnije
/// razišao sa onim što server stvarno šalje.
/// </summary>
public sealed record ReachProperties
{
    public required string SourceId { get; init; }

    public required string StationKey { get; init; }

    public required string Name { get; init; }

    public string? River { get; init; }

    /// <summary>Stupanj kao tekst (`Normal`, `Unknown`, …), ne broj.</summary>
    public required string Level { get; init; }

    /// <summary>Natpis iz legende agencije. Boja nikad nije jedini nosilac informacije
    /// (UI.md §5), pa tekst putuje uz nju.</summary>
    public required string LevelLabel { get; init; }

    /// <summary>Boja iz renderera agencije, ne naš izbor.</summary>
    public required string Color { get; init; }

    /// <summary>Doslovni tekst statusa iz izvora.</summary>
    public required string StatusLabelOriginal { get; init; }

    public decimal? ValueCm { get; init; }

    /// <summary>Kad je izmjereno. Nikad se ne miješa sa <see cref="FetchedAt"/>.</summary>
    public DateTimeOffset? MeasuredAt { get; init; }

    /// <summary>Kad smo povukli.</summary>
    public required DateTimeOffset FetchedAt { get; init; }

    public long? AgeMinutes { get; init; }

    public required long ExpectedIntervalMinutes { get; init; }

    /// <summary>Starost izražena u očekivanim intervalima. UI.md §2 iz ovoga bira prikaz.</summary>
    public double? AgeRatio { get; init; }

    public required string AgencyName { get; init; }

    public required string AgencyUrl { get; init; }

    public string? SourceUrl { get; init; }

    /// <summary>Zašto podatka nema. Prazno stanje mora reći šta se desilo (UI.md §7).</summary>
    public string? NoDataReason { get; init; }

    public IReadOnlyList<ReachThreshold>? Thresholds { get; init; }

    public string? ThresholdsDefinedBy { get; init; }
}

public sealed record ReachThreshold(string Label, decimal ValueCm, string? Level);

/// <summary>Zaglavlje kolekcije — UI iz njega zna kad su podaci povučeni i koliko je
/// dionica bez podatka, bez zasebnog poziva.</summary>
public sealed record ReachMeta
{
    public required string SourceId { get; init; }

    public required DateTimeOffset FetchedAt { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }

    public required int ReachCount { get; init; }

    public required int KnownCount { get; init; }

    public required int UnknownCount { get; init; }

    public required int WithoutGeometry { get; init; }
}

/// <summary>
/// GeoJSON kolekcija dionica. Geometrija je namjerno <see cref="JsonElement"/>-oblika u
/// implementaciji — poligon koji prođe kroz naš model pa se ponovo serijalizuje je poligon
/// koji smo mi prepisali.
/// </summary>
public sealed record ReachFeatureCollection
{
    [JsonPropertyName("type")]
    public string Type => "FeatureCollection";

    public required ReachMeta Meta { get; init; }

    public required IReadOnlyList<ReachFeature> Features { get; init; }
}

public sealed record ReachFeature
{
    [JsonPropertyName("type")]
    public string Type => "Feature";

    /// <summary>GeoJSON geometrija, prosljeđena od izvora bez prepisivanja.</summary>
    public required object Geometry { get; init; }

    public required ReachProperties Properties { get; init; }
}
