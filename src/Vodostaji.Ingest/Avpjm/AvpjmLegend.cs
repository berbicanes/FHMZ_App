using Vodostaji.Core;

namespace Vodostaji.Ingest.Avpjm;

/// <summary>
/// Legenda AVPJM-a. **Vlastita**, ne posuđena od AVP Save.
///
/// CLAUDE.md zabranjuje stapanje slojeva različitih agencija u jedan sloj sa jednom legendom,
/// i ovdje se vidi zašto to nije formalnost: AVP Sava ima pet stupnjeva sa zvaničnim bojama,
/// AVPJM nema nijedan. Zajednička legenda bi morala izmisliti nešto za jednu od njih.
/// </summary>
public sealed class AvpjmLegend : ISourceLegend
{
    /// <summary>
    /// Jedna boja za sve, jer ocjene nema.
    ///
    /// Namjerno nije zelena, žuta ni crvena — te boje kod AVP Save nose značenje, pa bi ista
    /// nijansa na jugu tvrdila nešto što agencija nije rekla. Plava kaže "izmjereno", ne
    /// "bezbjedno".
    /// </summary>
    public const string MeasuredColor = "#4a8fd4";

    /// <summary>Boja za stanicu bez podatka. Ista siva kao kod AVP Save — odsustvo podatka
    /// izgleda isto bez obzira ko ga nema.</summary>
    public const string NoDataColor = "#CCCCCC";

    public string Color(StationReading reading) =>
        reading.Measurement is not null ? MeasuredColor : NoDataColor;

    /// <summary>
    /// Natpis. "Nema ocjene" nije isto što i "nema podatka", i razlika mora biti u tekstu
    /// jer je u ovom sloju to jedina razlika koja postoji.
    /// </summary>
    public string Label(StationReading reading) =>
        reading.Measurement is not null ? "Izmjereno, bez ocjene opasnosti" : "Nema podatka";
}
