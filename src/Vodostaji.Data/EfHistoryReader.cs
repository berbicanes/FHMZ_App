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
        CancellationToken cancellationToken,
        ObservationParameter parameter = ObservationParameter.WaterLevel)
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
        var isLevel = parameter == ObservationParameter.WaterLevel;

        /*
         * Vodostaj i ostali parametri dolaze iz dvije tabele, i to je namjerno.
         *
         * Vodostaj nosi stupanj opasnosti koji je izvor tvrdio u tom trenutku; temperatura
         * ga nema i nikad neće imati. Jedna tabela bi tražila kolonu `Level` koja je za °C
         * besmislena i kolonu `ValueCm` koja za m³/s laže već imenom.
         */
        List<HistoryPoint> points;
        DateTimeOffset? collectingSince;
        string unit;

        if (isLevel)
        {
            points = await context.Measurements
                .AsNoTracking()
                .Where(m => m.SourceId == sourceId && m.StationKey == stationKey && m.MeasuredAt >= from)
                .OrderBy(m => m.MeasuredAt)
                .Select(m => new HistoryPoint(m.MeasuredAt, m.ValueCm, m.Level.ToString()))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // Najstarije mjerenje koje uopšte imamo, bez obzira na traženi raspon. Iz njega
            // UI zna reći "skupljamo od …" umjesto da prazan graf ostavi bez objašnjenja.
            collectingSince = await context.Measurements
                .AsNoTracking()
                .Where(m => m.SourceId == sourceId && m.StationKey == stationKey)
                .OrderBy(m => m.MeasuredAt)
                .Select(m => (DateTimeOffset?)m.MeasuredAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            unit = "cm";
        }
        else
        {
            points = await context.Observations
                .AsNoTracking()
                .Where(o => o.SourceId == sourceId && o.StationKey == stationKey &&
                            o.Parameter == parameter && o.MeasuredAt >= from)
                .OrderBy(o => o.MeasuredAt)
                // Ostali parametri nemaju stupanj opasnosti i ovdje se ne izmišlja nijedan.
                .Select(o => new HistoryPoint(o.MeasuredAt, o.Value, null))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            collectingSince = await context.Observations
                .AsNoTracking()
                .Where(o => o.SourceId == sourceId && o.StationKey == stationKey &&
                            o.Parameter == parameter)
                .OrderBy(o => o.MeasuredAt)
                .Select(o => (DateTimeOffset?)o.MeasuredAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            unit = await context.Observations
                .AsNoTracking()
                .Where(o => o.SourceId == sourceId && o.StationKey == stationKey &&
                            o.Parameter == parameter)
                .OrderByDescending(o => o.MeasuredAt)
                .Select(o => o.Unit)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false) ?? "";
        }

        // Šta se za ovu stanicu uopšte da nacrtati. Bez ovoga bi UI nudio izbor parametara
        // koje stanica nema, pa bi svaki drugi klik dao prazan graf.
        var recorded = await context.Observations
            .AsNoTracking()
            .Where(o => o.SourceId == sourceId && o.StationKey == stationKey)
            .GroupBy(o => new { o.Parameter, o.ParameterLabelOriginal, o.Unit })
            .Select(g => new HistoryParameter(
                g.Key.Parameter.ToString(), g.Key.ParameterLabelOriginal, g.Key.Unit))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasLevel = await context.Measurements
            .AsNoTracking()
            .AnyAsync(m => m.SourceId == sourceId && m.StationKey == stationKey, cancellationToken)
            .ConfigureAwait(false);

        var available = hasLevel
            ? new[] { new HistoryParameter("WaterLevel", "Vodostaj", "cm") }.Concat(recorded).ToList()
            : recorded;

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
            Parameter = parameter.ToString(),
            Unit = unit,
            Available = available,
            Points = points,
            // Pragovi pripadaju vodostaju. Crtati ih preko temperature bi značilo tvrditi
            // da 154 cm ima ikakvo značenje za 22 °C.
            Thresholds = isLevel ? thresholds : null,
            ThresholdsDefinedBy = isLevel ? state?.ThresholdsDefinedBy : null,
            AgencyName = station.AgencyName,
            CollectingSince = collectingSince,
        };
    }
}
