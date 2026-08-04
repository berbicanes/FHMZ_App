namespace Vodostaji.Core;

/// <summary>
/// Jedno mjerenje. Vrijednost i vrijeme mjerenja su neodvojivi — nema konstruktora
/// koji dopušta vrijednost bez vremena, jer bi to bio podatak koji se ne može pošteno
/// prikazati (zlatno pravilo 2).
///
/// <paramref name="MeasuredAt"/> je kad je izmjereno, u UTC-u. Nije kad smo povukli —
/// to je <c>FetchedAt</c> na <see cref="SourceFetchResult"/> i njih dvoje se ne miješaju.
/// </summary>
/// <param name="ValueCm">
/// Vodostaj u centimetrima. `decimal`, nikad `double` — AVP Sava šalje `esriFieldTypeSingle`
/// sa artefaktima poput `17.6000004`, i te artefakte ne smijemo ni pojačati ni sakriti.
/// Može biti negativan: kota nule letve nije dno rijeke.
/// </param>
/// <param name="MeasuredAt">Trenutak mjerenja, u UTC-u, kako ga je izvor prijavio.</param>
public sealed record Measurement(decimal ValueCm, DateTimeOffset MeasuredAt)
{
    /// <summary>Koliko je podatak star u odnosu na dati trenutak. Može biti negativno —
    /// izvor koji objavljuje u budućnost je nalaz, ne greška zaokruživanja.</summary>
    public TimeSpan AgeAt(DateTimeOffset now) => now - MeasuredAt;
}
