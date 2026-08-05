using Microsoft.EntityFrameworkCore;

namespace Vodostaji.Data;

/// <summary>Prethodno očitanje po stanici, za izvođenje trenda.</summary>
public sealed record PreviousReading(decimal ValueCm, DateTimeOffset MeasuredAt);

/// <summary>
/// Čita posljednje očitanje starije od trenutnog, po stanici.
///
/// Jedan upit za cijeli izvor umjesto po jedan po stanici. Kadenca je satna a dionica 45,
/// pa dva dana historije stane u par stotina redova — a 45 zasebnih upita svakih pet minuta
/// bi bilo opterećenje bez razloga.
/// </summary>
public sealed class EfPreviousReadingReader(VodostajiDbContext context)
{
    public async Task<IReadOnlyDictionary<string, PreviousReading>> ReadAsync(
        string sourceId,
        IReadOnlyDictionary<string, DateTimeOffset> currentByStation,
        CancellationToken cancellationToken)
    {
        if (currentByStation.Count == 0)
        {
            return new Dictionary<string, PreviousReading>();
        }

        // Dva dana pokrivaju i slučaj kad je izvor bio nedostupan preko noći.
        var from = currentByStation.Values.Min().AddDays(-2);

        var rows = await context.Measurements
            .AsNoTracking()
            .Where(m => m.SourceId == sourceId && m.MeasuredAt >= from)
            .Select(m => new { m.StationKey, m.ValueCm, m.MeasuredAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new Dictionary<string, PreviousReading>(StringComparer.Ordinal);

        foreach (var group in rows.GroupBy(r => r.StationKey, StringComparer.Ordinal))
        {
            if (!currentByStation.TryGetValue(group.Key, out var current))
            {
                continue;
            }

            // Strogo starije od trenutnog. Isto vrijeme je isti podatak, ne prethodni.
            var previous = group
                .Where(r => r.MeasuredAt < current)
                .OrderByDescending(r => r.MeasuredAt)
                .FirstOrDefault();

            if (previous is not null)
            {
                result[group.Key] = new PreviousReading(previous.ValueCm, previous.MeasuredAt);
            }
        }

        return result;
    }
}
