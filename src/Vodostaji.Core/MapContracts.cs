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

    /// <summary>
    /// Kota nule vodomjerne letve, u metrima nadmorske visine, kad je agencija objavi.
    ///
    /// Bez ovoga se **negativan vodostaj ne da objasniti**, a negativnih ima: Jala u Tuzli
    /// stoji na -23 cm. To nije greška ni u izvoru ni kod nas — vodostaj se mjeri u odnosu
    /// na nulu letve, a ne od dna korita, pa voda ispod te nule daje negativan broj. Nula
    /// letve je proizvoljno odabrana visina (za Tuzlu 221.921 m n.v.), i tek uz nju broj
    /// ima smisla.
    /// </summary>
    public decimal? GaugeZeroMetres { get; init; }

    /// <summary>Kad je izmjereno. Nikad se ne miješa sa <see cref="FetchedAt"/>.</summary>
    public DateTimeOffset? MeasuredAt { get; init; }

    /// <summary>Kad smo povukli.</summary>
    public required DateTimeOffset FetchedAt { get; init; }

    public long? AgeMinutes { get; init; }

    public required long ExpectedIntervalMinutes { get; init; }

    /// <summary>
    /// Da li je očekivani interval izmjeren iz historije ili je i dalje deklaracija adaptera.
    /// UI ne smije tvrditi "zastario" dok ritam stanice nije izmjeren.
    /// </summary>
    public required bool IntervalIsMeasured { get; init; }

    /// <summary>Uobičajeno kašnjenje objave kod ovog izvora. UI ga prikazuje da bi korisnik
    /// znao zašto ni najsvježiji podatak nije od maloprije.</summary>
    public required long PublicationLagMinutes { get; init; }

    /// <summary>
    /// Koliko je ciklusa propušteno, mjereno od trenutka kad je podatak realno mogao stići.
    /// Nula znači najsvježije moguće. UI.md §2 iz ovoga bira prikaz.
    /// </summary>
    public double? AgeRatio { get; init; }

    /// <summary>
    /// Prethodno očitanje iz naše historije, ako ga imamo.
    ///
    /// Trend se **izvodi**, ne dobija od izvora. AVP Sava ne objavljuje trend, pa uz strelicu
    /// moraju ići i tačna razlika i period preko kojeg je mjerena — bez toga je strelica naša
    /// tvrdnja koju korisnik ne može provjeriti.
    /// </summary>
    public decimal? PreviousValueCm { get; init; }

    public DateTimeOffset? PreviousMeasuredAt { get; init; }

    /// <summary>Razlika u odnosu na prethodno očitanje. Predznak nosi smjer.</summary>
    public decimal? ChangeCm { get; init; }

    /// <summary>
    /// Preko koliko minuta je razlika mjerena. Ako je izostalo nekoliko očitanja, razlika
    /// nije "za sat" nego "za pet sati", i strelica bez tog podatka pogrešno sugeriše brzinu.
    /// </summary>
    public long? ChangeOverMinutes { get; init; }

    /// <summary>Trend koji je **izvor objavio**, kad ga objavljuje. Ima prednost nad
    /// razlikom koju sami izračunamo iz dva očitanja (zlatno pravilo 3).</summary>
    public string? PublishedTrend { get; init; }

    /// <summary>Doslovna oznaka trenda iz izvora (`R`, `O`, `S`).</summary>
    public string? PublishedTrendLabel { get; init; }

    public required string AgencyName { get; init; }

    public required string AgencyUrl { get; init; }

    public string? SourceUrl { get; init; }

    /// <summary>Zašto podatka nema. Prazno stanje mora reći šta se desilo (UI.md §7).</summary>
    public string? NoDataReason { get; init; }

    public IReadOnlyList<ReachThreshold>? Thresholds { get; init; }

    public string? ThresholdsDefinedBy { get; init; }
}

/// <summary>
/// Prag kako se prikazuje, i kako ga izvor zove.
///
/// <see cref="Label"/> je ono što korisnik čita; <see cref="LabelOriginal"/> je doslovan
/// natpis izvora i **uvijek putuje uz njega**. Dva polja postoje da bi prevod ostao
/// provjerljiv: ako je bosanski naziv pogrešan, original stoji odmah do njega i vidi se.
/// Kad prevoda nema, oba su ista.
/// </summary>
public sealed record ReachThreshold(
    string Label,
    decimal ValueCm,
    string? Level,
    string LabelOriginal);

/// <summary>Prethodno očitanje, kako ga graditelj GeoJSON-a prima iz skladišta.</summary>
public sealed record PreviousMeasurement(decimal ValueCm, DateTimeOffset MeasuredAt);

/// <summary>Zaglavlje kolekcije — UI iz njega zna kad su podaci povučeni i koliko je
/// dionica bez podatka, bez zasebnog poziva.</summary>
public sealed record ReachMeta
{
    public required string SourceId { get; init; }

    public required DateTimeOffset FetchedAt { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }

    public required int ReachCount { get; init; }

    /// <summary>Sa ocjenom opasnosti.</summary>
    public required int KnownCount { get; init; }

    /// <summary>Bez ocjene opasnosti.</summary>
    public required int UnknownCount { get; init; }

    /// <summary>Sa izmjerenom vrijednošću. Kod AVPJM-a je 20 uz `KnownCount` nula.</summary>
    public required int MeasuredCount { get; init; }

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
