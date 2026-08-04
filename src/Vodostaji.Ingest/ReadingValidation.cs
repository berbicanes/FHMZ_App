using Vodostaji.Core;

namespace Vodostaji.Ingest;

public sealed record ValidatedReadings
{
    public required IReadOnlyList<StationReading> Readings { get; init; }

    /// <summary>Zapisi koje smo odbili, sa razlogom. Prazno je očekivano stanje.</summary>
    public required IReadOnlyList<SkippedStation> Rejected { get; init; }
}

/// <summary>
/// Provjere nad već isparsiranim zapisima, prije nego uđu u bazu.
///
/// Odbijeni zapis **ne nestaje** — postaje <see cref="StationReading.NoData"/> sa razlogom.
/// Tiho izbacivanje bi stanicu učinilo nevidljivom, a nevidljiva stanica u UI-u izgleda kao
/// da je nema, umjesto kao da o njoj ne znamo ništa.
/// </summary>
public static class ReadingValidation
{
    /// <summary>
    /// Koliko vremena u budućnost dopuštamo prije nego zapis proglasimo nepouzdanim.
    ///
    /// Nije nula zbog razlike u satovima između njihovog i našeg servera. Nije ni velika,
    /// jer je timestamp u budućnosti tačno ono što nam je kod AVPJM-a otkrilo pogrešnu
    /// pretpostavku o zoni — ako se pojavi ovdje, pretpostavka je pogrešna i podatak se
    /// ne smije prikazati kao svjež.
    /// </summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    public static ValidatedReadings Apply(
        IReadOnlyList<StationReading> readings,
        DateTimeOffset now,
        TimeSpan? futureTolerance = null)
    {
        var tolerance = futureTolerance ?? FutureTolerance;
        var accepted = new List<StationReading>(readings.Count);
        var rejected = new List<SkippedStation>();

        foreach (var reading in readings)
        {
            if (reading.Measurement is not { } measurement)
            {
                accepted.Add(reading);
                continue;
            }

            var ahead = measurement.MeasuredAt - now;
            if (ahead <= tolerance)
            {
                accepted.Add(reading);
                continue;
            }

            var reason =
                $"Vrijeme mjerenja je {ahead.TotalMinutes:F0} min u budućnosti — "
                + "pretpostavka o vremenskoj zoni izvora je vjerovatno pogrešna.";

            rejected.Add(new SkippedStation(reading.Station.StationKey, reason));

            accepted.Add(new StationReading.NoData
            {
                Station = reading.Station,
                StatusLabelOriginal = reading.StatusLabelOriginal,
                Thresholds = reading.Thresholds,
                Reason = reason,
            });
        }

        return new ValidatedReadings { Readings = accepted, Rejected = rejected };
    }
}
