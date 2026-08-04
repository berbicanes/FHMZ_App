namespace Vodostaji.Core;

/// <summary>
/// Jedan prag, kako ga izvor imenuje.
/// </summary>
/// <param name="LabelOriginal">
/// Naziv doslovno iz izvora — `STANDBY_STAT`, `redovna_obrana`, `ordinary_value`.
/// Prikazuje se korisniku, jer prag bez imena agencije koja ga je postavila ne znači ništa.
/// </param>
/// <param name="ValueCm">Vrijednost u centimetrima.</param>
/// <param name="Level">
/// Stupanj na koji se prag odnosi, kad je to iz dokumentacije izvora nedvosmisleno.
/// **Null je legitiman i čest** — kod AVPJM-a `kontinuirana_obrana` nema očit parnjak, i
/// izmišljanje mapiranja bi bilo isto što i izmišljanje statusa.
/// </param>
public sealed record Threshold(string LabelOriginal, decimal ValueCm, AlertLevel? Level = null);

/// <summary>
/// Pragovi koje je definisao hidrolog. Čuvaju se da bi se **prikazali**, ne da bi se iz njih
/// izvodio status — to zabranjuje zlatno pravilo 3. Ako izvor kaže "Normalno" dok je vrijednost
/// iznad praga, prikazuje se "Normalno" i pragovi, pa neka korisnik vidi neslaganje.
///
/// Namjerno nije lista fiksnih polja. AVP Sava ima četiri praga, AVPJM tri sa drugim imenima,
/// RHMZ RS dva. Svođenje na jedan oblik bi značilo da negdje izmišljamo prag kojeg nema ili
/// prešućujemo onaj koji postoji.
/// </summary>
public sealed record Thresholds
{
    public required IReadOnlyList<Threshold> Values { get; init; }

    /// <summary>
    /// Ime agencije koja je pragove postavila. UI ih mora prikazati uz ime — pragovi nisu
    /// univerzalni, nego odluka konkretne institucije za konkretnu dionicu.
    /// </summary>
    public required string DefinedBy { get; init; }

    public bool IsEmpty => Values.Count == 0;

    public static Thresholds None(string definedBy) => new()
    {
        Values = [],
        DefinedBy = definedBy,
    };
}
