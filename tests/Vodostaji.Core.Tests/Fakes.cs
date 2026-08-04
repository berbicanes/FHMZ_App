using Vodostaji.Core;

namespace Vodostaji.Core.Tests;

/// <summary>
/// Sat kojim upravlja test. Vlastiti umjesto `Microsoft.Extensions.TimeProvider.Testing` —
/// nekoliko redova ne opravdava novu zavisnost.
/// </summary>
internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Izvor kojim upravlja test — vraća unaprijed pripremljene odgovore redom.</summary>
internal sealed class FakeSource(params Func<SourceFetchResult>[] responses) : IStationDataSource
{
    private int _call;

    public int Calls => _call;

    public string SourceId => "fake";

    public Attribution Attribution { get; } = new()
    {
        AgencyName = "Test agencija",
        AgencyUrl = new Uri("https://example.invalid"),
    };

    public SourceClock Clock { get; } = new()
    {
        Convention = ClockConvention.Utc,
        Evidence = "test",
    };

    public TimeSpan MinimumFetchInterval { get; init; } = TimeSpan.FromMinutes(15);

    public Task<SourceFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        var response = responses[Math.Min(_call, responses.Length - 1)];
        _call++;
        return Task.FromResult(response());
    }
}

/// <summary>Skladište u memoriji. Bilježi i koliko je puta upisano, da se može tvrditi
/// da se pri padu izvora **ništa** nije upisalo.</summary>
internal sealed class FakeStore : IReadingStore
{
    private readonly Dictionary<string, IReadOnlyList<StationReading>> _bySource = [];

    public int SaveCount { get; private set; }

    public SourceFetchResult? LastSaved { get; private set; }

    public Task SaveAsync(SourceFetchResult result, CancellationToken cancellationToken)
    {
        SaveCount++;
        LastSaved = result;
        _bySource[result.SourceId] = result.Readings;
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(string sourceId, CancellationToken cancellationToken) =>
        Task.FromResult(_bySource.TryGetValue(sourceId, out var readings) ? readings.Count : 0);

    public void Seed(string sourceId, params StationReading[] readings) =>
        _bySource[sourceId] = readings;
}

internal static class Build
{
    public static readonly Attribution Attribution = new()
    {
        AgencyName = "Test agencija",
        AgencyUrl = new Uri("https://example.invalid"),
    };

    public static Station Station(string key = "1") => new()
    {
        SourceId = "fake",
        StationKey = key,
        Name = $"Stanica {key}",
        ExpectedInterval = TimeSpan.FromHours(1),
        Attribution = Attribution,
    };

    public static StationReading Measured(DateTimeOffset measuredAt, decimal value = 100m, string key = "1") =>
        new StationReading.Measured
        {
            Station = Station(key),
            StatusLabelOriginal = "Standby",
            ClaimedLevel = AlertLevel.Normal,
            MeasuredValue = new Measurement(value, measuredAt),
        };

    public static SourceFetchResult Ok(DateTimeOffset fetchedAt, params StationReading[] readings) => new()
    {
        SourceId = "fake",
        FetchedAt = fetchedAt,
        Readings = readings,
    };

    public static SourceFetchResult Down(DateTimeOffset fetchedAt, string reason = "timeout") => new()
    {
        SourceId = "fake",
        FetchedAt = fetchedAt,
        Readings = [],
        FailureReason = reason,
    };
}
