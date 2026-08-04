namespace Vodostaji.Core;

/// <summary>Geografska tačka u WGS84. Uvijek iz geometrije koju izvor vrati, nikad iz
/// atributa `x`/`y` — kod AVP Save ti atributi miješaju Gauss-Krüger zone i kod tri stanice
/// su ose zamijenjene (SOURCES.md §1.2).</summary>
public sealed record Coordinates(double Latitude, double Longitude);

/// <summary>
/// Stanica ili dionica koju pratimo. Jedan izvor, jedan ključ unutar tog izvora.
/// Ključ nikad nije globalan — `HID_ID` kod AVP Save i `id` kod AVPJM-a se ne poklapaju.
/// </summary>
public sealed record Station
{
    /// <summary>Identifikator izvora, npr. `avp-sava`. Ide u svaki ingest log.</summary>
    public required string SourceId { get; init; }

    /// <summary>Ključ stanice unutar izvora. Jedinstven samo u kombinaciji sa
    /// <see cref="SourceId"/>.</summary>
    public required string StationKey { get; init; }

    /// <summary>Naziv kako ga izvor piše, doslovno. Bez naših ispravki dijakritike.</summary>
    public required string Name { get; init; }

    public string? River { get; init; }

    /// <summary>Null je legitimno: kod AVP Save jedna od 102 stanice nema geometriju.</summary>
    public Coordinates? Coordinates { get; init; }

    /// <summary>Kota nule vodomjerne letve. Bez nje se vodostaj ne može prevesti u apsolutnu
    /// kotu — nedostaje kod 13 od 102 stanice AVP Save.</summary>
    public decimal? GaugeZero { get; init; }

    /// <summary>
    /// Koliko često izvor **stvarno** osvježava ovu stanicu. Ide po stanici, ne po izvoru:
    /// kod FHMZBIH-a se Zenica mijenja satno a Bihać znatno rjeđe (SOURCES.md §3).
    /// Ovo nosi prikaz starosti u UI-u, pa optimistična vrijednost ovdje laže korisniku.
    /// </summary>
    public required TimeSpan ExpectedInterval { get; init; }

    public required Attribution Attribution { get; init; }
}
