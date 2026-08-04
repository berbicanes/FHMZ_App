namespace Vodostaji.Core;

/// <summary>
/// Ime agencije i link na izvor. LEGAL.md §2.1 traži atribuciju **po stanici**, ne u footeru,
/// pa ovo putuje sa svakom stanicom umjesto da stoji negdje u konfiguraciji stranice.
/// </summary>
public sealed record Attribution
{
    public required string AgencyName { get; init; }

    public required Uri AgencyUrl { get; init; }

    /// <summary>Link na tačnu stranicu ili sloj odakle podatak dolazi, kad postoji.</summary>
    public Uri? SourceUrl { get; init; }
}
