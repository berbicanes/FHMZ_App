using Vodostaji.Core;
using Vodostaji.Ingest;

namespace Vodostaji.Api;

/// <summary>
/// Vozi sve izvore u pozadini.
///
/// Svaki izvor je vlastiti <see cref="SourcePipeline"/> sa vlastitim osiguračem i vlastitim
/// izlaznim fajlom. Petlja hvata izuzetak **po izvoru**, pa pad jednog ne prekida obradu
/// ostalih — zlatno pravilo 5 nije komentar nego oblik ove petlje.
/// </summary>
public sealed class IngestHostedService(
    IServiceProvider services,
    IEnumerable<SourcePipeline> pipelines,
    TimeProvider timeProvider,
    ILogger<IngestHostedService> logger)
    : BackgroundService
{
    /// <summary>Runner sam odbija pokušaj koji je prerano — razmak prema izvoru je njegova
    /// odgovornost, ne ovog rasporeda.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(5);

    private readonly SourcePipeline[] _pipelines = [.. pipelines];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Ingest pokrenut: {Count} izvora, provjera svakih {Tick}.", _pipelines.Length, Tick);

        using var timer = new PeriodicTimer(Tick, timeProvider);

        do
        {
            foreach (var pipeline in _pipelines)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await RunOneAsync(pipeline, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Ovdje se pravilo 5 stvarno provodi: izuzetak jednog izvora ne izlazi
                    // iz njegove iteracije.
                    logger.LogError(ex, "Ciklus izvora {SourceId} je pao.", pipeline.SourceId);
                }
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task RunOneAsync(SourcePipeline pipeline, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();

        // SourceId u svakom ingest logu (CLAUDE.md → Konvencije).
        using var _ = logger.BeginScope(new Dictionary<string, object>
        {
            ["SourceId"] = pipeline.SourceId,
        });

        await pipeline.PrepareAsync(cancellationToken).ConfigureAwait(false);

        var store = scope.ServiceProvider.GetRequiredService<IReadingStore>();
        var outcome = await pipeline.Runner.RunOnceAsync(store, cancellationToken).ConfigureAwait(false);

        switch (outcome)
        {
            case IngestOutcome.Succeeded:
                logger.LogInformation(
                    "{SourceId}: {Known} sa podatkom, {Unknown} bez, {Skipped} preskočeno.",
                    pipeline.SourceId,
                    pipeline.Runner.Status.KnownCount,
                    pipeline.Runner.Status.UnknownCount,
                    pipeline.Runner.LastSuccessfulResult?.Skipped.Count ?? 0);

                await pipeline.PublishAsync(scope.ServiceProvider, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case IngestOutcome.Failed:
            case IngestOutcome.RejectedEmpty:
                logger.LogWarning(
                    "{SourceId} nije dao upotrebljiv odgovor ({Outcome}): {Reason}. "
                    + "Stari podatak je zadržan.",
                    pipeline.SourceId, outcome, pipeline.Runner.Status.LastFailureReason);
                break;

            case IngestOutcome.SkippedCircuitOpen:
                logger.LogInformation(
                    "{SourceId}: osigurač otvoren, izvor se pušta na miru.", pipeline.SourceId);
                break;

            case IngestOutcome.SkippedTooSoon:
            default:
                break;
        }
    }
}
