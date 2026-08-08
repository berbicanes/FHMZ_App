using Vodostaji.Core;

namespace Vodostaji.Ingest.Wiski;

/// <summary>
/// Legenda WISKI sloja.
///
/// <para>
/// Kao i kod AVPJM-a i FHMZBIH-a, ovaj izvor **ne objavljuje stupanj opasnosti**, pa boja ne
/// smije nositi ocjenu. Nosi samo razliku „imamo broj" / „nemamo broj". Treća boja je nužna
/// jer se slojevi agencija nikad ne stapaju — ali boja nikad nije jedini nosilac (UI.md §5),
/// pa se ovaj sloj na mapi razlikuje i **debelim tamnim prstenom**, dok AVPJM ima tanku
/// ivicu a FHMZBIH svijetli prsten.
/// </para>
/// </summary>
public sealed class WiskiLegend : ISourceLegend
{
    /// <summary>Ljubičasta, dovoljno daleko od plave AVPJM-a i tirkizne FHMZBIH-a.</summary>
    public const string MeasuredColor = "#7a4bbd";

    /// <summary>Ista siva kao svugdje — odsustvo podatka izgleda isto bez obzira ko ga nema.</summary>
    public const string NoDataColor = "#CCCCCC";

    public string Color(StationReading reading) =>
        reading.Measurement is not null ? MeasuredColor : NoDataColor;

    /// <summary>
    /// Natpis mora razlikovati tri stanja, ne dva.
    ///
    /// Stanica koja mjeri temperaturu ali ne vodostaj **nije** stanica bez podatka — podatak
    /// postoji, samo nije onaj po kojem je aplikacija nazvana. Spojiti to sa „nema podatka"
    /// bila bi ista greška kao spojiti „ne znam" i „normalno", samo u drugom smjeru.
    /// </summary>
    public string Label(StationReading reading) => reading switch
    {
        { Measurement: not null } => "Izmjereno, bez ocjene opasnosti",
        { Observations.Count: > 0 } => "Bez vodostaja — mjeri druge parametre",
        _ => "Nema podatka",
    };
}
