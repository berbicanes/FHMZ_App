namespace Vodostaji.Core;

/// <summary>
/// Jedno očitanje u historiji. Vrijednost i vrijeme mjerenja, ništa izvedeno.
///
/// <para>
/// Polje se zove <c>Value</c>, ne <c>ValueCm</c>: isti graf sada crta i °C i m³/s, a ime sa
/// jedinicom u sebi bi na temperaturi lagalo. Jedinica stoji jednom, na
/// <see cref="ReachHistory.Unit"/>.
/// </para>
///
/// <para>
/// <c>Level</c> je null za sve osim vodostaja — ostali parametri nemaju stupanj opasnosti i
/// ovdje se nijedan ne izmišlja.
/// </para>
/// </summary>
public sealed record HistoryPoint(DateTimeOffset MeasuredAt, decimal Value, string? Level);

/// <summary>
/// Historija jedne dionice, sa pragovima i imenom agencije koja ih je postavila.
///
/// Pragovi putuju uz seriju jer ih graf crta kao horizontalne linije, a UI.md §3 traži da uz
/// njih **uvijek** stoji ime agencije. Prag bez imena onoga ko ga je odredio izgleda kao naš.
/// </summary>
/// <summary>Jedan parametar koji stanica stvarno ima u historiji, sa svojim natpisom.</summary>
public sealed record HistoryParameter(string Parameter, string Label, string Unit);

public sealed record ReachHistory
{
    public required string SourceId { get; init; }

    public required string StationKey { get; init; }

    public required string Name { get; init; }

    public string? River { get; init; }

    /// <summary>Koliko dana unazad je traženo.</summary>
    public required int Days { get; init; }

    /// <summary>Koji parametar graf prikazuje. `WaterLevel` je podrazumijevani.</summary>
    public required string Parameter { get; init; }

    /// <summary>Jedinica prikazanog parametra, doslovno iz izvora (`cm`, `°C`, `m³/s`).</summary>
    public required string Unit { get; init; }

    /// <summary>
    /// Šta se sve za ovu stanicu može nacrtati. UI iz ovoga gradi izbor, umjesto da nagađa
    /// koje parametre stanica ima i nudi prazne grafove.
    /// </summary>
    public IReadOnlyList<HistoryParameter> Available { get; init; } = [];

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
