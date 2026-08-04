using Vodostaji.Core;

namespace Vodostaji.Ingest;

/// <summary>
/// Osigurač nad jednim izvorom.
///
/// Ne postoji da bi zaštitio nas — postoji da bi zaštitio **njih**. Server koji vraća greške
/// je vjerovatno server u nevolji, a zlatno pravilo 6 kaže da je njihova infrastruktura javna
/// imovina. Hlađenje raste udvostručavanjem, do gornje granice.
/// </summary>
public sealed class CircuitBreaker(
    int failureThreshold,
    TimeSpan initialCooldown,
    TimeSpan maxCooldown,
    TimeProvider timeProvider)
{
    private readonly TimeSpan _initialCooldown = initialCooldown;
    private DateTimeOffset? _openedUntil;
    private TimeSpan _cooldown = initialCooldown;

    public CircuitState State { get; private set; } = CircuitState.Closed;

    public int ConsecutiveFailures { get; private set; }

    public DateTimeOffset? OpenUntil => _openedUntil;

    /// <summary>
    /// Smijemo li pokušati sada. Kad hlađenje istekne, prelazi u <see cref="CircuitState.HalfOpen"/>
    /// i propušta tačno jedan pokušaj.
    /// </summary>
    public bool AllowAttempt()
    {
        if (State != CircuitState.Open)
        {
            return true;
        }

        if (_openedUntil is { } until && timeProvider.GetUtcNow() < until)
        {
            return false;
        }

        State = CircuitState.HalfOpen;
        return true;
    }

    public void RecordSuccess()
    {
        State = CircuitState.Closed;
        ConsecutiveFailures = 0;
        _openedUntil = null;
        _cooldown = _initialCooldown;
    }

    public void RecordFailure()
    {
        ConsecutiveFailures++;

        // Pad u poluotvorenom stanju znači da se izvor nije oporavio — odmah nazad u otvoreno,
        // sa dužim hlađenjem, bez ponovnog brojanja do praga.
        if (State == CircuitState.HalfOpen)
        {
            _cooldown = Min(_cooldown + _cooldown, maxCooldown);
            Open();
            return;
        }

        if (ConsecutiveFailures >= failureThreshold)
        {
            Open();
        }
    }

    private void Open()
    {
        State = CircuitState.Open;
        _openedUntil = timeProvider.GetUtcNow() + _cooldown;
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
