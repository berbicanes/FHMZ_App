namespace Vodostaji.Core;

/// <summary>Stanica koju nismo uspjeli pročitati. Nije izuzetak nego zapis — kontrolna lista
/// traži da neuspjeh parsiranja preskoči stanicu i logira se, a ne da sruši cijeli run.</summary>
public sealed record SkippedStation(string StationKey, string Reason);

/// <summary>
/// Rezultat jednog povlačenja sa jednog izvora.
///
/// <see cref="FetchedAt"/> je kad smo povukli. Vrijeme mjerenja živi na
/// <see cref="Measurement.MeasuredAt"/> i to dvoje se nikad ne miješa — prikazivanje
/// <c>FetchedAt</c> kao vremena mjerenja je najlakši način da se prekrši zlatno pravilo 2.
/// </summary>
public sealed record SourceFetchResult
{
    public required string SourceId { get; init; }

    public required DateTimeOffset FetchedAt { get; init; }

    public required IReadOnlyList<StationReading> Readings { get; init; }

    /// <summary>Stanice preskočene u ovom runu, sa razlogom.</summary>
    public IReadOnlyList<SkippedStation> Skipped { get; init; } = [];

    /// <summary>
    /// Ako je izvor pao u cjelini. Prazan run nije isto što i run sa nula stanica —
    /// ovo razlikuje "nismo mogli pitati" od "pitali smo i nema ničega".
    /// </summary>
    public string? FailureReason { get; init; }

    public bool Succeeded => FailureReason is null;

    /// <summary>
    /// Koliko stanica ima **ocjenu opasnosti**. Nije isto što i broj mjerenja: AVPJM daje
    /// 20 vrijednosti i nijednu ocjenu, jer stupanj ne objavljuje javnosti (SOURCES.md §2.1).
    /// Dok su postojala samo dva izvora sa istim ponašanjem, razlika se nije vidjela.
    /// </summary>
    public int KnownCount => Readings.Count(r => r.Level.IsKnown());

    public int UnknownCount => Readings.Count(r => !r.Level.IsKnown());

    /// <summary>Koliko stanica ima **izmjerenu vrijednost**, bez obzira na ocjenu.</summary>
    public int MeasuredCount => Readings.Count(r => r.Measurement is not null);

    /// <summary>Koliko ih je bez ijedne vrijednosti.</summary>
    public int WithoutMeasurementCount => Readings.Count(r => r.Measurement is null);
}
