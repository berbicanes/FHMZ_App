namespace Vodostaji.Core;

/// <summary>
/// Pragovi koje je definisao hidrolog. Čuvaju se da bi se **prikazali**, ne da bi se iz njih
/// izvodio status — to zabranjuje zlatno pravilo 3. Ako izvor kaže "Normalno" dok je vrijednost
/// iznad praga, prikazuje se "Normalno" i pragovi, pa neka korisnik vidi neslaganje.
///
/// Svaki prag može nedostajati. Kod AVPJM-a su `redovna_obrana` i `vanredna_obrana` prazni za
/// Mostar, a kod AVP Save su sva četiri popunjena. Prazan prag je nepoznat prag.
/// </summary>
public sealed record Thresholds
{
    public decimal? RegularDefenceCm { get; init; }

    public decimal? OutstandingDefenceCm { get; init; }

    public decimal? EmergencyCm { get; init; }

    /// <summary>
    /// Ime agencije koja je pragove postavila. UI ih mora prikazati uz ime — pragovi nisu
    /// univerzalni, nego odluka konkretne institucije za konkretnu dionicu.
    /// </summary>
    public required string DefinedBy { get; init; }

    public bool IsEmpty =>
        RegularDefenceCm is null && OutstandingDefenceCm is null && EmergencyCm is null;
}
