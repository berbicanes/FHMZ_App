using Vodostaji.Core;
using Vodostaji.Data;
using Vodostaji.Ingest;
using Vodostaji.Ingest.AvpSava;

namespace Vodostaji.Api;

/// <summary>
/// Vozi ingest u pozadini i prepisuje GeoJSON koji mapa čita.
///
/// Jedan runner po izvoru; kad ih bude više, svaki dobija svoj i pad jednog ne dodiruje
/// ostale (zlatno pravilo 5).
/// </summary>
public sealed class IngestHostedService(
    IServiceProvider services,
    SourceIngestRunner runner,
    AvpSavaGeometrySource geometrySource,
    AvpSavaStationSource stationSource,
    ReachMapFile mapFile,
    StationMapFile stationFile,
    TimeProvider timeProvider,
    ILogger<IngestHostedService> logger)
    : BackgroundService
{
    /// <summary>Koliko često provjeravamo. Runner sam odbija pokušaj koji je prerano —
    /// razmak prema izvoru je njegova odgovornost, ne ovog rasporeda.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(5);

    private IReadOnlyDictionary<string, string> _geometry = new Dictionary<string, string>();
    private DateTimeOffset? _geometryFetchedAt;
    private DateTimeOffset? _stationsFetchedAt;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Ingest pokrenut. Provjera svakih {Tick}.", Tick);

        using var timer = new PeriodicTimer(Tick, timeProvider);

        do
        {
            try
            {
                await RunCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Ciklus koji padne ne smije oboriti servis — sljedeći pokušaj je za 5 minuta.
                logger.LogError(ex, "Ingest ciklus je pao.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        await EnsureGeometryAsync(cancellationToken).ConfigureAwait(false);
        await EnsureStationsAsync(cancellationToken).ConfigureAwait(false);

        // Store se rješava po ciklusu jer DbContext ima scoped životni vijek, a
        // pozadinski servis je singleton.
        using var scope = services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IReadingStore>();

        var outcome = await runner.RunOnceAsync(store, cancellationToken).ConfigureAwait(false);

        // SourceId u svakom ingest logu (CLAUDE.md → Konvencije).
        using var _ = logger.BeginScope(new Dictionary<string, object>
        {
            ["SourceId"] = runner.Status.SourceId,
        });

        switch (outcome)
        {
            case IngestOutcome.Succeeded:
                logger.LogInformation(
                    "Povučeno: {Known} sa podatkom, {Unknown} bez, {Skipped} preskočeno.",
                    runner.Status.KnownCount, runner.Status.UnknownCount,
                    runner.LastSuccessfulResult?.Skipped.Count ?? 0);
                await PublishMapAsync(cancellationToken).ConfigureAwait(false);
                break;

            case IngestOutcome.Failed:
            case IngestOutcome.RejectedEmpty:
                logger.LogWarning(
                    "Izvor nije dao upotrebljiv odgovor ({Outcome}): {Reason}. "
                    + "Stari podatak je zadržan.",
                    outcome, runner.Status.LastFailureReason);
                break;

            case IngestOutcome.SkippedCircuitOpen:
                logger.LogInformation("Osigurač otvoren, izvor se pušta na miru.");
                break;

            case IngestOutcome.SkippedTooSoon:
                break;

            default:
                break;
        }
    }

    private async Task EnsureGeometryAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        if (_geometryFetchedAt is { } fetched &&
            now - fetched < AvpSavaGeometrySource.RefreshInterval &&
            _geometry.Count > 0)
        {
            return;
        }

        try
        {
            _geometry = await geometrySource.FetchAsync(cancellationToken).ConfigureAwait(false);
            _geometryFetchedAt = now;
            logger.LogInformation("Geometrija osvježena: {Count} dionica.", _geometry.Count);
        }
        catch (Exception ex)
        {
            // Stara geometrija je i dalje dobra — poligoni se ne mijenjaju često.
            logger.LogWarning(ex, "Geometrija nije osvježena, koristi se prethodna.");
        }
    }

    /// <summary>
    /// Registar mjernih mjesta. Osvježava se jednom dnevno i **ne zavisi od uspjeha ingesta** —
    /// stanice postoje i kad izvor te minute ne odgovara na upit o stanju.
    /// </summary>
    private async Task EnsureStationsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        if (_stationsFetchedAt is { } fetched &&
            now - fetched < AvpSavaStationSource.RefreshInterval &&
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
            // Stari registar ostaje. Stanice se ne sele.
            logger.LogWarning(ex, "Registar stanica nije osvježen, koristi se prethodni.");
        }
    }

    private async Task PublishMapAsync(CancellationToken cancellationToken)
    {
        if (runner.LastSuccessfulResult is not { } result || _geometry.Count == 0)
        {
            return;
        }

        var geoJson = AvpSavaReachGeoJson.Build(result, _geometry, timeProvider.GetUtcNow());
        await mapFile.WriteAsync(geoJson, cancellationToken).ConfigureAwait(false);
    }
}
