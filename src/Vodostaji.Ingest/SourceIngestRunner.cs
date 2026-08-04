using Vodostaji.Core;

namespace Vodostaji.Ingest;

public enum IngestOutcome
{
    /// <summary>Prerano — nije prošao <see cref="IStationDataSource.MinimumFetchInterval"/>.</summary>
    SkippedTooSoon = 0,

    /// <summary>Osigurač je otvoren, izvor se pušta na miru.</summary>
    SkippedCircuitOpen,

    Succeeded,

    /// <summary>Izvor je pao. Stari podatak ostaje netaknut.</summary>
    Failed,

    /// <summary>
    /// Odgovor je formalno uspio ali stigao prazan, a ranije je imao stanica.
    /// Tretira se kao pad, jer prazan odgovor koji prepiše punu bazu je najtiši način
    /// da cijela mapa posivi bez ijedne greške u logu.
    /// </summary>
    RejectedEmpty,
}

public sealed record IngestOptions
{
    public int FailureThreshold { get; init; } = 3;

    public TimeSpan InitialCooldown { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan MaxCooldown { get; init; } = TimeSpan.FromHours(4);
}

/// <summary>
/// Vodi jedan izvor: poštuje njegov rate limit, drži osigurač, validira i upisuje.
///
/// Jedan runner po izvoru, bez ijedne zajedničke promjenljive — pad jednog izvora zato ne
/// može dodirnuti ostale (zlatno pravilo 5).
/// </summary>
public sealed class SourceIngestRunner
{
    private readonly IStationDataSource _source;
    private readonly TimeProvider _time;
    private readonly CircuitBreaker _breaker;

    private DateTimeOffset? _lastAttemptAt;
    private DateTimeOffset? _lastSuccessAt;
    private string? _lastFailureReason;
    private int _knownCount;
    private int _unknownCount;

    public SourceIngestRunner(
        IStationDataSource source,
        TimeProvider timeProvider,
        IngestOptions? options = null)
    {
        var settings = options ?? new IngestOptions();

        _source = source;
        _time = timeProvider;
        _breaker = new CircuitBreaker(
            settings.FailureThreshold, settings.InitialCooldown, settings.MaxCooldown, timeProvider);
    }

    /// <summary>
    /// Zadnje uspješno povlačenje, već validirano. Ostaje netaknuto kad izvor padne —
    /// ono što se od njega gradi (GeoJSON za mapu) time zadržava stari podatak sa starim
    /// vremenom, umjesto da nestane.
    /// </summary>
    public SourceFetchResult? LastSuccessfulResult { get; private set; }

    public SourceStatus Status => new()
    {
        SourceId = _source.SourceId,
        AgencyName = _source.Attribution.AgencyName,
        LastAttemptAt = _lastAttemptAt,
        LastSuccessAt = _lastSuccessAt,
        ConsecutiveFailures = _breaker.ConsecutiveFailures,
        Circuit = _breaker.State,
        LastFailureReason = _lastFailureReason,
        KnownCount = _knownCount,
        UnknownCount = _unknownCount,
        ClockEvidence = _source.Clock.Evidence,
    };

    /// <summary>
    /// Skladište ulazi po pozivu, ne u konstruktor. Runner živi koliko i proces, a
    /// `DbContext` koliko i jedan zahtjev — držanje skladišta u polju bi ga zaključalo
    /// na prvi scope koji ga je vidio.
    /// </summary>
    public async Task<IngestOutcome> RunOnceAsync(
        IReadingStore store, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();

        // LEGAL.md §2.5 je obećanje izvoru, ne podešavanje. Provjera ide prije osigurača,
        // jer i pokušaj koji bi osigurač propustio mora poštovati razmak.
        if (_lastAttemptAt is { } last && now - last < _source.MinimumFetchInterval)
        {
            return IngestOutcome.SkippedTooSoon;
        }

        if (!_breaker.AllowAttempt())
        {
            return IngestOutcome.SkippedCircuitOpen;
        }

        _lastAttemptAt = now;

        var result = await _source.FetchAsync(cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return Fail(result.FailureReason);
        }

        if (result.Readings.Count == 0 &&
            await store.CountAsync(_source.SourceId, cancellationToken).ConfigureAwait(false) > 0)
        {
            _breaker.RecordFailure();
            _lastFailureReason =
                "Odgovor je uspio ali je stigao prazan, a ranije je imao stanica. "
                + "Stari podatak je zadržan.";

            return IngestOutcome.RejectedEmpty;
        }

        var validated = ReadingValidation.Apply(result.Readings, _time.GetUtcNow());

        var toStore = result with
        {
            Readings = validated.Readings,
            Skipped = [.. result.Skipped, .. validated.Rejected],
        };

        await store.SaveAsync(toStore, cancellationToken).ConfigureAwait(false);

        _breaker.RecordSuccess();
        _lastSuccessAt = now;
        _lastFailureReason = null;
        LastSuccessfulResult = toStore;
        _knownCount = toStore.KnownCount;
        _unknownCount = toStore.UnknownCount;

        return IngestOutcome.Succeeded;
    }

    private IngestOutcome Fail(string? reason)
    {
        _breaker.RecordFailure();
        _lastFailureReason = reason ?? "Nepoznat razlog.";

        // Ništa se ne upisuje i ništa se ne briše. Stari podatak ostaje sa svojim
        // poštenim timestampom, pa ga UI prikazuje kao star — a ne kao nepostojeći.
        return IngestOutcome.Failed;
    }
}
