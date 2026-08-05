using Vodostaji.Core;

namespace Vodostaji.Ingest.Fhmzbih;

/// <summary>
/// Legenda FHMZBIH-a. **Vlastita**, iako je semantika ista kao kod AVPJM-a.
///
/// Boja je drugačija namjerno: korisnik koji vidi dvije različite nijanse pita se čije su, a
/// korisnik koji vidi istu nijansu pretpostavlja da je ista agencija i ista skala. Nijedna od
/// ovih boja nije iz skale AVP Save — te boje tamo nose značenje, i posuđivanje bi tvrdilo
/// nešto što ova agencija nije rekla.
/// </summary>
public sealed class FhmzbihLegend : ISourceLegend
{
    public const string MeasuredColor = "#3aa8a0";
    public const string NoDataColor = "#CCCCCC";

    public string Color(StationReading reading) =>
        reading.Measurement is not null ? MeasuredColor : NoDataColor;

    public string Label(StationReading reading) =>
        reading.Measurement is not null ? "Izmjereno, bez ocjene opasnosti" : "Nema podatka";
}
