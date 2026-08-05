namespace Vodostaji.Core;

/// <summary>
/// Svojstva jedne stanice u registarskom sloju.
///
/// **Ovdje namjerno nema statusa, boje ni vrijednosti.** Registar kaže gdje su mjerna mjesta,
/// ne kakvo je stanje na njima — a `HYDRO_ID` na dionicama ne pokazuje na ovaj registar
/// (SOURCES.md §1.7), pa se stanje ne može ni prikačiti. Dodavanje neutralne boje koja bi
/// mogla proći kao "sve u redu" bilo bi kršenje zlatnog pravila 1 na najtiši mogući način.
/// </summary>
public sealed record StationProperties
{
    public required string SourceId { get; init; }

    /// <summary>`HID_ID`. Null za jednu stanicu AVP Save koja ga nema.</summary>
    public string? StationKey { get; init; }

    public required string Name { get; init; }

    /// <summary>Opis lokacije kako ga agencija piše — `rijeka Bosna - uzvodno od ušća Krivaje`.
    /// Ovo je i polje po kojem korisnik iz Maglaja traži "Maglaj" (UI.md §4).</summary>
    public string? Location { get; init; }

    /// <summary>`Automatska stanica` ili `Vodomjerna letva`. Letva se očitava ručno, pa
    /// korisnik treba znati razliku.</summary>
    public string? StationType { get; init; }

    /// <summary>Kota nule vodomjerne letve. Nedostaje kod 13 od 102 stanice.</summary>
    public decimal? GaugeZero { get; init; }

    public int? GaugeBoardCount { get; init; }

    public required string AgencyName { get; init; }

    public required string AgencyUrl { get; init; }
}

public sealed record StationMeta
{
    public required string SourceId { get; init; }

    public required DateTimeOffset FetchedAt { get; init; }

    public required int StationCount { get; init; }

    /// <summary>Stanice bez geometrije ne mogu na mapu. Broj se objavljuje umjesto da se
    /// prešuti — mapa sa 101 tačkom uz registar od 102 mora znati objasniti razliku.</summary>
    public required int WithoutGeometry { get; init; }

    public required int WithoutGaugeZero { get; init; }

    /// <summary>Zapisi bez naziva. Ne mogu se prikazati jer se ne mogu imenovati,
    /// ali se broje — tiho ispuštanje bi značilo da registar izgleda manji nego što jeste.</summary>
    public required int WithoutName { get; init; }
}

public sealed record StationFeatureCollection
{
    public required StationMeta Meta { get; init; }

    public required IReadOnlyList<StationFeature> Features { get; init; }
}

public sealed record StationFeature
{
    public required object Geometry { get; init; }

    public required StationProperties Properties { get; init; }
}
