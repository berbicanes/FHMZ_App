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

    /// <summary>
    /// Koliko obično prođe od trenutka mjerenja do trenutka kad podatak postane dostupan nama.
    ///
    /// Odvojeno od <see cref="ExpectedInterval"/> namjerno. AVP Sava mjeri na sat ali objavljuje
    /// 85–115 minuta kasnije, pa je i savršeno zdravo očitanje uvijek staro oko dva sata. Bez
    /// ovog polja svaka dionica trajno stoji kao "kasni", korisnik se navikne da je signal
    /// uvijek upaljen, i prestane ga gledati — što je gore nego da ga nema.
    ///
    /// Starost se zato mjeri **od trenutka kad je podatak realno mogao stići**, ne od mjerenja.
    /// Ono što se time prikazuje je broj propuštenih ciklusa, a to je ono što UI.md §2 i traži.
    /// </summary>
    public TimeSpan TypicalPublicationLag { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Da li je <see cref="ExpectedInterval"/> **izmjeren** iz naše historije, ili je i dalje
    /// deklaracija adaptera o kadenci cijelog izvora.
    ///
    /// Razlika je bitna zato što je nepoznat ritam neizmjeren, ne brz. Dok se ne izmjeri,
    /// zadržava se kraći deklarisani interval — greška u tom smjeru prikaže stanicu kao
    /// zakašnjelu kad nije, a greška u suprotnom smjeru prikaže **ugašenu stanicu kao svježu**.
    /// Od to dvoje, drugo je opasno.
    ///
    /// UI zato ne tvrdi "zastario" dok ritam nije izmjeren, nego kaže koliko je podatak star
    /// i da ritam još ne znamo.
    /// </summary>
    public bool IntervalIsMeasured { get; init; }

    /// <summary>
    /// Koliko je ciklusa propušteno. Nula znači da je podatak najsvježiji koji uopšte može biti.
    /// Vraća null kad mjerenja nema — odsustvo podatka nije stepen starosti.
    /// </summary>
    public double? MissedCycles(DateTimeOffset? measuredAt, DateTimeOffset now)
    {
        if (measuredAt is not { } measured || ExpectedInterval <= TimeSpan.Zero)
        {
            return null;
        }

        var beyondLag = (now - measured) - TypicalPublicationLag;
        return beyondLag <= TimeSpan.Zero ? 0 : beyondLag / ExpectedInterval;
    }

    public required Attribution Attribution { get; init; }
}
