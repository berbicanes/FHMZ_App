using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vodostaji.Core;

namespace Vodostaji.Data;

/// <summary>Čita historiju mjerenja za graf 7/30 dana (UI.md §3).</summary>
public sealed class EfHistoryReader(VodostajiDbContext context)
{
    public async Task<ReachHistory?> ReadAsync(
        string sourceId,
        string stationKey,
        int days,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var station = await context.Stations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.SourceId == sourceId && s.StationKey == stationKey, cancellationToken)
            .ConfigureAwait(false);

        if (station is null)
        {
            return null;
        }

        var from = now.AddDays(-days);

        var points = await context.Measurements
            .AsNoTracking()
            .Where(m => m.SourceId == sourceId && m.StationKey == stationKey && m.MeasuredAt >= from)
            .OrderBy(m => m.MeasuredAt)
            .Select(m => new HistoryPoint(m.MeasuredAt, m.ValueCm, m.Level.ToString()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Najstarije mjerenje koje uopšte imamo, bez obzira na traženi raspon. Iz njega UI
        // zna reći "skupljamo od …" umjesto da prazan graf ostavi bez objašnjenja.
        var collectingSince = await context.Measurements
            .AsNoTracking()
            .Where(m => m.SourceId == sourceId && m.StationKey == stationKey)
            .OrderBy(m => m.MeasuredAt)
            .Select(m => (DateTimeOffset?)m.MeasuredAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var state = await context.StationStates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.SourceId == sourceId && s.StationKey == stationKey, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ReachThreshold>? thresholds = null;
        if (state?.ThresholdsJson is { Length: > 0 } json)
        {
            try
            {
                var stored = JsonSerializer.Deserialize<List<Threshold>>(json);
                thresholds = stored?
                    .Select(t => new ReachThreshold(
                        ThresholdNames.Display(t.LabelOriginal),
                        t.ValueCm,
                        t.Level?.ToString(),
                        t.LabelOriginal))
                    .ToList();
            }
            catch (JsonException)
            {
                // Nečitljivi pragovi se izostavljaju, ne izmišljaju. Graf bez linija je
                // pošteniji od grafa sa pogrešnim linijama.
                thresholds = null;
            }
        }

        return new ReachHistory
        {
            SourceId = sourceId,
            StationKey = stationKey,
            Name = station.Name,
            River = station.River,
            Days = days,
            Points = points,
            Thresholds = thresholds,
            ThresholdsDefinedBy = state?.ThresholdsDefinedBy,
            AgencyName = station.AgencyName,
            CollectingSince = collectingSince,
        };
    }
}
