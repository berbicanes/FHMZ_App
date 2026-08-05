namespace Vodostaji.Core;

/// <summary>Jedno očitanje u historiji. Vrijednost i vrijeme mjerenja, ništa izvedeno.</summary>
public sealed record HistoryPoint(DateTimeOffset MeasuredAt, decimal ValueCm, string Level);

/// <summary>
/// Historija jedne dionice, sa pragovima i imenom agencije koja ih je postavila.
///
/// Pragovi putuju uz seriju jer ih graf crta kao horizontalne linije, a UI.md §3 traži da uz
/// njih **uvijek** stoji ime agencije. Prag bez imena onoga ko ga je odredio izgleda kao naš.
/// </summary>
public sealed record ReachHistory
{
    public required string SourceId { get; init; }

    public required string StationKey { get; init; }

    public required string Name { get; init; }

    public string? River { get; init; }

    /// <summary>Koliko dana unazad je traženo.</summary>
    public required int Days { get; init; }

    /// <summary>Rastuće po vremenu mjerenja.</summary>
    public required IReadOnlyList<HistoryPoint> Points { get; init; }

    public IReadOnlyList<ReachThreshold>? Thresholds { get; init; }

    public string? ThresholdsDefinedBy { get; init; }

    public required string AgencyName { get; init; }

    /// <summary>
    /// Otkad uopšte imamo zapise za ovu dionicu. Prazan graf zbog toga što tek skupljamo
    /// podatke nije isto što i prazan graf zbog toga što je izvor pao — a korisnik razliku
    /// ne može pogoditi ako mu se ne kaže.
    /// </summary>
    public DateTimeOffset? CollectingSince { get; init; }
}
