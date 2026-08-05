using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vodostaji.Core;

namespace Vodostaji.Data;

/// <summary>
/// Upisuje rezultate povlačenja. Nema nijedan `Remove` ni `ExecuteDelete` — ugovor
/// <see cref="IReadingStore"/> namjerno nema brisanje, pa ga ni implementacija nema odakle
/// pozvati (zlatno pravilo 5).
/// </summary>
public sealed class EfReadingStore(VodostajiDbContext context) : IReadingStore
{
    public async Task SaveAsync(SourceFetchResult result, CancellationToken cancellationToken)
    {
        foreach (var reading in result.Readings)
        {
            await UpsertStationAsync(reading.Station, result.FetchedAt, cancellationToken)
                .ConfigureAwait(false);

            await UpsertStateAsync(reading, result.FetchedAt, cancellationToken)
                .ConfigureAwait(false);

            await AppendMeasurementAsync(reading, result.FetchedAt, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<int> CountAsync(string sourceId, CancellationToken cancellationToken) =>
        context.StationStates.CountAsync(s => s.SourceId == sourceId, cancellationToken);

    private async Task UpsertStationAsync(
        Station station, DateTimeOffset fetchedAt, CancellationToken cancellationToken)
    {
        var row = await context.Stations
            .FirstOrDefaultAsync(
                s => s.SourceId == station.SourceId && s.StationKey == station.StationKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            context.Stations.Add(new StationRow
            {
                SourceId = station.SourceId,
                StationKey = station.StationKey,
                Name = station.Name,
                River = station.River,
                Latitude = station.Coordinates?.Latitude,
                Longitude = station.Coordinates?.Longitude,
                GaugeZero = station.GaugeZero,
                ExpectedIntervalSeconds = (long)station.ExpectedInterval.TotalSeconds,
                PublicationLagSeconds = (long)station.TypicalPublicationLag.TotalSeconds,
                AgencyName = station.Attribution.AgencyName,
                AgencyUrl = station.Attribution.AgencyUrl.ToString(),
                SourceUrl = station.Attribution.SourceUrl?.ToString(),
                FirstSeenAt = fetchedAt,
                LastSeenAt = fetchedAt,
            });

            return;
        }

        row.Name = station.Name;
        row.River = station.River;
        row.GaugeZero = station.GaugeZero;
        row.ExpectedIntervalSeconds = (long)station.ExpectedInterval.TotalSeconds;
        row.PublicationLagSeconds = (long)station.TypicalPublicationLag.TotalSeconds;
        row.LastSeenAt = fetchedAt;

        // Koordinate se ne brišu ako ih odgovor ovaj put nije donio. Jedna stanica AVP Save
        // nema geometriju, i to što je nema danas ne znači da je nikad nismo imali.
        if (station.Coordinates is { } coordinates)
        {
            row.Latitude = coordinates.Latitude;
            row.Longitude = coordinates.Longitude;
        }
    }

    private async Task UpsertStateAsync(
        StationReading reading, DateTimeOffset fetchedAt, CancellationToken cancellationToken)
    {
        var row = await context.StationStates
            .FirstOrDefaultAsync(
                s => s.SourceId == reading.Station.SourceId && s.StationKey == reading.Station.StationKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new StationStateRow
            {
                SourceId = reading.Station.SourceId,
                StationKey = reading.Station.StationKey,
                FetchedAt = fetchedAt,
                Level = reading.Level,
                StatusLabelOriginal = reading.StatusLabelOriginal,
            };

            context.StationStates.Add(row);
        }

        row.FetchedAt = fetchedAt;
        row.Level = reading.Level;
        row.StatusLabelOriginal = reading.StatusLabelOriginal;
        row.ValueCm = reading.Measurement?.ValueCm;
        row.MeasuredAt = reading.Measurement?.MeasuredAt;
        row.NoDataReason = reading is StationReading.NoData noData ? noData.Reason : null;
        row.ThresholdsDefinedBy = reading.Thresholds?.DefinedBy;
        row.ThresholdsJson = reading.Thresholds is { IsEmpty: false } thresholds
            ? JsonSerializer.Serialize(thresholds.Values)
            : null;
    }

    private async Task AppendMeasurementAsync(
        StationReading reading, DateTimeOffset fetchedAt, CancellationToken cancellationToken)
    {
        if (reading.Measurement is not { } measurement)
        {
            return;
        }

        // Jedinstveni indeks bi ovo svejedno odbio, ali provjera ovdje znači da se
        // ne oslanjamo na izuzetak kao na tok upravljanja.
        var exists = await context.Measurements
            .AnyAsync(
                m => m.SourceId == reading.Station.SourceId &&
                     m.StationKey == reading.Station.StationKey &&
                     m.MeasuredAt == measurement.MeasuredAt,
                cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        context.Measurements.Add(new MeasurementRow
        {
            SourceId = reading.Station.SourceId,
            StationKey = reading.Station.StationKey,
            ValueCm = measurement.ValueCm,
            MeasuredAt = measurement.MeasuredAt,
            Level = reading.Level,
            FirstFetchedAt = fetchedAt,
        });
    }
}
