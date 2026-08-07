using Microsoft.EntityFrameworkCore;

namespace Vodostaji.Data;

/// <summary>
/// Računa stvarni razmak između mjerenja, po stanici, iz naše historije.
///
/// Uzima se **medijan**, ne prosjek: jedna duga pauza zbog pada izvora pomjeri prosjek i
/// učini stanicu trajno "svježom" nego što jeste. Medijan opisuje ritam, prosjek opisuje i
/// prekide.
/// </summary>
public sealed class EfObservedIntervalReader(VodostajiDbContext context)
{
    /// <summary>Prozor posmatranja. Duži bi pamtio ritam koji izvor više nema.</summary>
    public static TimeSpan Window => TimeSpan.FromDays(14);

    public async Task<IReadOnlyDictionary<string, TimeSpan>> ReadAsync(
        string sourceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var from = now - Window;

        var rows = await context.Measurements
            .AsNoTracking()
            .Where(m => m.SourceId == sourceId && m.MeasuredAt >= from)
            .OrderBy(m => m.StationKey).ThenBy(m => m.MeasuredAt)
            .Select(m => new { m.StationKey, m.MeasuredAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);

        foreach (var group in rows.GroupBy(r => r.StationKey, StringComparer.Ordinal))
        {
            var times = group.Select(r => r.MeasuredAt).ToList();
            if (times.Count <= Core.ObservedIntervals.MinimumSamples)
            {
                continue;
            }

            var gaps = new List<double>(times.Count - 1);
            for (var i = 1; i < times.Count; i++)
            {
                var minutes = (times[i] - times[i - 1]).TotalMinutes;
                if (minutes > 0)
                {
                    gaps.Add(minutes);
                }
            }

            if (gaps.Count < Core.ObservedIntervals.MinimumSamples)
            {
                continue;
            }

            gaps.Sort();
            var median = gaps.Count % 2 == 1
                ? gaps[gaps.Count / 2]
                : (gaps[(gaps.Count / 2) - 1] + gaps[gaps.Count / 2]) / 2;

            result[group.Key] = TimeSpan.FromMinutes(median);
        }

        return result;
    }
}
