namespace Vodostaji.Core;

/// <summary>
/// Šta je izmjereno. Vodostaj je i dalje srce aplikacije, ali nije jedino što izvori šalju.
///
/// <para>
/// <c>Unknown = 0</c> je namjerno prvo: <c>default(ObservationParameter)</c> pada na
/// „ne znam šta je ovo", nikad na konkretno mjerenje. Isti razlog kao kod
/// <see cref="AlertLevel"/> — polje koje se zaboravi popuniti mora ispasti bezopasno.
/// </para>
///
/// <para>
/// Nepoznat parametar se **ne odbacuje**. Kad izvor sutra doda nešto novo, vrijednost se i
/// dalje prikazuje pod imenom koje joj izvor daje; samo se ne pretvara ni u šta naše.
/// </para>
/// </summary>
public enum ObservationParameter
{
    Unknown = 0,
    WaterLevel,
    Flow,
    WaterTemperature,
    AirTemperature,
    Precipitation,
    GroundwaterLevel,
    GroundwaterTemperature,
}

/// <summary>
/// Jedno mjerenje jednog parametra, sa **vlastitim** vremenom.
///
/// <para>
/// Vrijeme je po mjerenju, ne po stanici, i to nije sitnica. U WISKI izvozu AVP Save ista
/// stanica u istom trenutku nosi temperaturu vode staru pola sata i nivo podzemne vode star
/// 132 dana (SOURCES.md §4.5). Jedan timestamp na nivou stanice bi jedno od to dvoje
/// pretvorio u laž — a ako bi to bilo starije, prikazali bismo četiri mjeseca star podatak
/// kao trenutno stanje, što je zlatno pravilo 2.
/// </para>
///
/// <para>
/// <see cref="Unit"/> je doslovno ono što izvor pošalje (<c>cm</c>, <c>°C</c>, <c>m³/s</c>,
/// <c>mm</c>, <c>m</c>). Ne preračunava se i ne normalizuje: pretvaranje jedinice je tiha
/// prilika da se pogriješi za faktor sto, a dobitak je nula.
/// </para>
/// </summary>
public sealed record Observation
{
    public required ObservationParameter Parameter { get; init; }

    /// <summary>Naziv parametra kako ga izvor piše. Prikazuje se kad naš naziv ne postoji.</summary>
    public required string ParameterLabelOriginal { get; init; }

    public required decimal Value { get; init; }

    public required string Unit { get; init; }

    public required DateTimeOffset MeasuredAt { get; init; }

    /// <summary>Koliko je ovo mjerenje staro. Može biti negativno — izvor koji objavljuje u
    /// budućnost je nalaz, ne greška zaokruživanja.</summary>
    public TimeSpan AgeAt(DateTimeOffset now) => now - MeasuredAt;
}
