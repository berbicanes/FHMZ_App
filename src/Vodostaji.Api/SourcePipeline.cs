using Vodostaji.Core;
using Vodostaji.Data;
using Vodostaji.Ingest;
using Vodostaji.Ingest.AvpSava;
using Vodostaji.Ingest.Avpjm;

namespace Vodostaji.Api;

/// <summary>
/// Jedan izvor od kraja do kraja: vlastiti runner, vlastiti osigurač, vlastiti izlazni fajl.
///
/// Ovo postoji da bi izolacija izvora bila **strukturna, a ne stvar pažnje**. Dodavanje trećeg
/// izvora znači dodavanje jednog pipelinea; nema mjesta na kojem bi se dva izvora mogla
/// nehotice preplesti (zlatno pravilo 5).
/// </summary>
public abstract class SourcePipeline(SourceIngestRunner runner, TimeProvider timeProvider)
{
    public SourceIngestRunner Runner { get; } = runner;

    protected TimeProvider Time { get; } = timeProvider;

    public string SourceId => Runner.Status.SourceId;

    /// <summary>Priprema koja se radi rjeđe od ingesta — geometrija, registri. Pad ovdje
    /// ne smije spriječiti povlačenje vrijednosti.</summary>
    public virtual Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Prepisuje GeoJSON koji mapa čita, iz zadnjeg uspješnog povlačenja.</summary>
    public abstract Task PublishAsync(IServiceProvider scoped, CancellationToken cancellationToken);
}

/// <summary>AVP Sava — poligoni dionica, geometrija se povlači odvojeno i rijetko.</summary>
public sealed class AvpSavaPipeline(
    SourceIngestRunner runner,
    AvpSavaGeometrySource geometrySource,
    AvpSavaStationSource stationSource,
    ReachMapFile mapFile,
    StationMapFile stationFile,
    TimeProvider timeProvider,
    ILogger<AvpSavaPipeline> logger)
    : SourcePipeline(runner, timeProvider)
{
    private IReadOnlyDictionary<string, string> _geometry = new Dictionary<string, string>();
    private DateTimeOffset? _geometryFetchedAt;
    private DateTimeOffset? _stationsFetchedAt;

    public override async Task PrepareAsync(CancellationToken cancellationToken)
    {
        var now = Time.GetUtcNow();

        if (_geometryFetchedAt is not { } geometryAt ||
            now - geometryAt >= AvpSavaGeometrySource.RefreshInterval ||
            _geometry.Count == 0)
        {
            try
            {
                _geometry = await geometrySource.FetchAsync(cancellationToken).ConfigureAwait(false);
                _geometryFetchedAt = now;
                logger.LogInformation("Geometrija osvježena: {Count} dionica.", _geometry.Count);
            }
            catch (Exception ex)
            {
                // Poligoni se ne mijenjaju često; stara geometrija je i dalje dobra.
                logger.LogWarning(ex, "Geometrija nije osvježena, koristi se prethodna.");
            }
        }

        if (_stationsFetchedAt is { } stationsAt &&
            now - stationsAt < AvpSavaStationSource.RefreshInterval &&
            stationFile.Exists)
        {
            return;
        }

        try
        {
            var geoJson = await stationSource.FetchGeoJsonAsync(now, cancellationToken)
                .ConfigureAwait(false);

            await stationFile.WriteAsync(geoJson, cancellationToken).ConfigureAwait(false);
            _stationsFetchedAt = now;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Registar stanica nije osvježen, koristi se prethodni.");
        }
    }

    public override async Task PublishAsync(IServiceProvider scoped, CancellationToken cancellationToken)
    {
        if (Runner.LastSuccessfulResult is not { } result || _geometry.Count == 0)
        {
            return;
        }

        var previous = await PreviousReadings
            .ReadAsync(scoped, SourceId, result, cancellationToken)
            .ConfigureAwait(false);

        var geoJson = AvpSavaReachGeoJson.Build(result, _geometry, Time.GetUtcNow(), previous);
        await mapFile.WriteAsync(geoJson, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Izvor koji objavljuje tačke, a ne dionice: AVPJM i FHMZBIH.
///
/// Zajednički pipeline, ali **vlastita legenda i vlastiti fajl** po izvoru. Dijeljenje koda
/// nije stapanje slojeva — svaki i dalje ide u svoj sloj sa svojom legendom.
/// </summary>
public sealed class PointSourcePipeline(
    SourceIngestRunner runner,
    ISourceLegend legend,
    PointMapFile mapFile,
    TimeProvider timeProvider)
    : SourcePipeline(runner, timeProvider)
{
    public override async Task PublishAsync(IServiceProvider scoped, CancellationToken cancellationToken)
    {
        if (Runner.LastSuccessfulResult is not { } result)
        {
            return;
        }

        var previous = await PreviousReadings
            .ReadAsync(scoped, SourceId, result, cancellationToken)
            .ConfigureAwait(false);

        var geoJson = PointSourceGeoJson.Build(result, legend, Time.GetUtcNow(), previous);
        await mapFile.WriteAsync(geoJson, cancellationToken).ConfigureAwait(false);
    }
}

internal static class PreviousReadings
{
    /// <summary>
    /// Prethodna očitanja se čitaju **poslije** upisa, pa "prethodno" znači prije trenutnog
    /// mjerenja, a ne prije ovog ciklusa.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, PreviousMeasurement>> ReadAsync(
        IServiceProvider scoped,
        string sourceId,
        SourceFetchResult result,
        CancellationToken cancellationToken)
    {
        var currentByStation = result.Readings
            .Where(r => r.Measurement is not null)
            .ToDictionary(
                r => r.Station.StationKey,
                r => r.Measurement!.MeasuredAt,
                StringComparer.Ordinal);

        var reader = scoped.GetRequiredService<EfPreviousReadingReader>();
        var previous = await reader.ReadAsync(sourceId, currentByStation, cancellationToken)
            .ConfigureAwait(false);

        return previous.ToDictionary(
            pair => pair.Key,
            pair => new PreviousMeasurement(pair.Value.ValueCm, pair.Value.MeasuredAt),
            StringComparer.Ordinal);
    }
}
