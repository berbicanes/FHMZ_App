namespace Vodostaji.Core;

/// <summary>Stanje osigurača nad jednim izvorom.</summary>
public enum CircuitState
{
    /// <summary>Normalan rad — pokušaji prolaze.</summary>
    Closed = 0,

    /// <summary>Izvor je pao dovoljno puta da ga pustimo na miru. Njihova infrastruktura je
    /// javna imovina (zlatno pravilo 6); uporno gađanje servera koji pada je najgori oblik
    /// nepristojnosti prema izvoru.</summary>
    Open,

    /// <summary>Hlađenje je prošlo, propuštamo jedan pokušaj da vidimo je li se oporavio.</summary>
    HalfOpen,
}

/// <summary>
/// Ono što `/api/v1/sources` prikazuje o jednom izvoru, i što UI koristi da kaže korisniku
/// da jug mape nije prazan nego bez podatka.
///
/// <see cref="LastSuccessAt"/> i <see cref="LastAttemptAt"/> su namjerno razdvojeni:
/// izvor koji se pokušava svakih 15 minuta a nije uspio od jučer nije isto što i svjež izvor.
/// </summary>
public sealed record SourceStatus
{
    public required string SourceId { get; init; }

    public required string AgencyName { get; init; }

    public DateTimeOffset? LastAttemptAt { get; init; }

    public DateTimeOffset? LastSuccessAt { get; init; }

    public int ConsecutiveFailures { get; init; }

    public CircuitState Circuit { get; init; }

    public string? LastFailureReason { get; init; }

    /// <summary>Koliko stanica je u zadnjem uspješnom povlačenju imalo podatak.</summary>
    public int KnownCount { get; init; }

    /// <summary>Koliko ih je bilo bez podatka. Ovo se prikazuje, ne skriva.</summary>
    public int UnknownCount { get; init; }

    /// <summary>Dokaz za pretpostavku o vremenskoj zoni, iz <see cref="SourceClock.Evidence"/>.
    /// Ide u API da bi bilo javno provjerljivo šta smo pretpostavili i zašto.</summary>
    public required string ClockEvidence { get; init; }

    public bool IsHealthy => Circuit == CircuitState.Closed && ConsecutiveFailures == 0;
}
