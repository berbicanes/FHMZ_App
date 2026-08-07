namespace Vodostaji.Core;

/// <summary>
/// Zamjenjuje deklarisani <see cref="Station.ExpectedInterval"/> onim koji je **izmjeren**
/// iz naše vlastite historije.
///
/// Adapter deklariše kadencu izvora kao cjeline, jer drugo ne zna unaprijed. Ali stanice se
/// ne javljaju istim ritmom: kod FHMZBIH-a se većina javlja svaka dva sata, Reljevo svakih
/// pet, a Bihać **jednom dnevno**. Sa jednim intervalom za sve, Bihać trajno stoji kao
/// "zastario" iako radi tačno po svom rasporedu — signal koji je uvijek upaljen korisnik
/// prestane gledati.
///
/// Mjerenje pobjeđuje deklaraciju, ali tek kad ima dovoljno uzoraka da nije slučajnost.
/// </summary>
public static class ObservedIntervals
{
    /// <summary>
    /// Ispod ovoga se deklarisana vrijednost ne dira. Dva-tri očitanja mogu opisati pauzu,
    /// ne ritam.
    /// </summary>
    public const int MinimumSamples = 5;

    public static SourceFetchResult WithObservedIntervals(
        this SourceFetchResult result,
        IReadOnlyDictionary<string, TimeSpan> observedByStation)
    {
        if (observedByStation.Count == 0)
        {
            return result;
        }

        var readings = new List<StationReading>(result.Readings.Count);

        foreach (var reading in result.Readings)
        {
            if (!observedByStation.TryGetValue(reading.Station.StationKey, out var observed) ||
                observed <= TimeSpan.Zero)
            {
                readings.Add(reading);
                continue;
            }

            var station = reading.Station with
            {
                ExpectedInterval = observed,
                IntervalIsMeasured = true,
            };

            readings.Add(reading switch
            {
                StationReading.Measured measured => measured with { Station = station },
                StationReading.NoData noData => noData with { Station = station },
                _ => reading,
            });
        }

        return result with { Readings = readings };
    }
}
