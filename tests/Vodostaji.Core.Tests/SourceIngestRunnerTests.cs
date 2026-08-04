using Vodostaji.Core;
using Vodostaji.Ingest;

namespace Vodostaji.Core.Tests;

public class SourceIngestRunnerTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Uspjesno_povlacenje_se_upisuje()
    {
        var clock = new TestClock(Start);
        var source = new FakeSource(() => Build.Ok(Start, Build.Measured(Start.AddMinutes(-10))));
        var store = new FakeStore();
        var runner = new SourceIngestRunner(source, clock);

        var outcome = await runner.RunOnceAsync(store, CancellationToken.None);

        Assert.Equal(IngestOutcome.Succeeded, outcome);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(Start, runner.Status.LastSuccessAt);
        Assert.True(runner.Status.IsHealthy);
    }

    [Fact]
    public async Task Rate_limit_se_postuje_jer_je_obecanje_izvoru()
    {
        var clock = new TestClock(Start);
        var source = new FakeSource(() => Build.Ok(Start, Build.Measured(Start)))
        {
            MinimumFetchInterval = TimeSpan.FromMinutes(15),
        };
        var store = new FakeStore();
        var runner = new SourceIngestRunner(source, clock);

        Assert.Equal(IngestOutcome.Succeeded, await runner.RunOnceAsync(store, CancellationToken.None));

        clock.Advance(TimeSpan.FromMinutes(14));
        Assert.Equal(IngestOutcome.SkippedTooSoon, await runner.RunOnceAsync(store, CancellationToken.None));
        Assert.Equal(1, source.Calls);

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(IngestOutcome.Succeeded, await runner.RunOnceAsync(store, CancellationToken.None));
        Assert.Equal(2, source.Calls);
    }

    [Fact]
    public async Task Pad_izvora_ne_dira_stari_podatak()
    {
        var clock = new TestClock(Start);
        var store = new FakeStore();
        store.Seed("fake", Build.Measured(Start.AddHours(-3)));

        var runner = new SourceIngestRunner(
            new FakeSource(() => Build.Down(Start, "server ne odgovara")), clock);

        var outcome = await runner.RunOnceAsync(store, CancellationToken.None);

        Assert.Equal(IngestOutcome.Failed, outcome);

        // Ništa nije upisano i ništa nije obrisano — stari podatak ostaje sa svojim timestampom.
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(1, await store.CountAsync("fake", CancellationToken.None));
        Assert.Equal("server ne odgovara", runner.Status.LastFailureReason);
        Assert.Null(runner.Status.LastSuccessAt);
    }

    [Fact]
    public async Task Prazan_odgovor_ne_smije_prepisati_punu_bazu()
    {
        var clock = new TestClock(Start);
        var store = new FakeStore();
        store.Seed("fake", Build.Measured(Start.AddHours(-1), key: "1"), Build.Measured(Start, key: "2"));

        var runner = new SourceIngestRunner(new FakeSource(() => Build.Ok(Start)), clock);

        var outcome = await runner.RunOnceAsync(store, CancellationToken.None);

        Assert.Equal(IngestOutcome.RejectedEmpty, outcome);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(2, await store.CountAsync("fake", CancellationToken.None));
    }

    [Fact]
    public async Task Prazan_odgovor_na_praznoj_bazi_je_legitiman()
    {
        var clock = new TestClock(Start);
        var store = new FakeStore();
        var runner = new SourceIngestRunner(new FakeSource(() => Build.Ok(Start)), clock);

        // Prvo pokretanje protiv izvora koji stvarno nema nijednu stanicu nije greška.
        Assert.Equal(IngestOutcome.Succeeded, await runner.RunOnceAsync(store, CancellationToken.None));
    }

    [Fact]
    public async Task Osiguraci_se_otvara_nakon_praga_i_prestaje_gadjati_izvor()
    {
        var clock = new TestClock(Start);
        var source = new FakeSource(() => Build.Down(Start)) { MinimumFetchInterval = TimeSpan.Zero };
        var store = new FakeStore();
        var runner = new SourceIngestRunner(source, clock,
            new IngestOptions { FailureThreshold = 3, InitialCooldown = TimeSpan.FromMinutes(15) });

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(IngestOutcome.Failed, await runner.RunOnceAsync(store, CancellationToken.None));
            clock.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.Equal(CircuitState.Open, runner.Status.Circuit);

        // Otvoren osigurač znači da se izvor više ne dira.
        var callsBefore = source.Calls;
        Assert.Equal(IngestOutcome.SkippedCircuitOpen, await runner.RunOnceAsync(store, CancellationToken.None));
        Assert.Equal(callsBefore, source.Calls);
    }

    [Fact]
    public async Task Nakon_hladjenja_prolazi_jedan_pokusaj_i_oporavak_zatvara_osiguraci()
    {
        var clock = new TestClock(Start);
        var attempt = 0;
        var source = new FakeSource(() =>
            ++attempt <= 3 ? Build.Down(Start) : Build.Ok(Start, Build.Measured(Start)))
        {
            MinimumFetchInterval = TimeSpan.Zero,
        };

        var store = new FakeStore();
        var runner = new SourceIngestRunner(source, clock,
            new IngestOptions { FailureThreshold = 3, InitialCooldown = TimeSpan.FromMinutes(15) });

        for (var i = 0; i < 3; i++)
        {
            await runner.RunOnceAsync(store, CancellationToken.None);
        }

        Assert.Equal(CircuitState.Open, runner.Status.Circuit);

        clock.Advance(TimeSpan.FromMinutes(16));

        Assert.Equal(IngestOutcome.Succeeded, await runner.RunOnceAsync(store, CancellationToken.None));
        Assert.Equal(CircuitState.Closed, runner.Status.Circuit);
        Assert.Equal(0, runner.Status.ConsecutiveFailures);
    }

    [Fact]
    public async Task Ponovni_pad_u_poluotvorenom_stanju_produzava_hladjenje()
    {
        var clock = new TestClock(Start);
        var source = new FakeSource(() => Build.Down(Start)) { MinimumFetchInterval = TimeSpan.Zero };
        var store = new FakeStore();
        var runner = new SourceIngestRunner(source, clock,
            new IngestOptions
            {
                FailureThreshold = 2,
                InitialCooldown = TimeSpan.FromMinutes(15),
                MaxCooldown = TimeSpan.FromHours(4),
            });

        await runner.RunOnceAsync(store, CancellationToken.None);
        await runner.RunOnceAsync(store, CancellationToken.None);
        Assert.Equal(CircuitState.Open, runner.Status.Circuit);

        // Prvo hlađenje je 15 min; nakon pada u poluotvorenom postaje 30.
        clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(IngestOutcome.Failed, await runner.RunOnceAsync(store, CancellationToken.None));

        clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(IngestOutcome.SkippedCircuitOpen, await runner.RunOnceAsync(store, CancellationToken.None));

        clock.Advance(TimeSpan.FromMinutes(15));
        Assert.Equal(IngestOutcome.Failed, await runner.RunOnceAsync(store, CancellationToken.None));
    }

    [Fact]
    public async Task Mjerenje_iz_buducnosti_postaje_nepoznato_a_ne_nestaje()
    {
        var clock = new TestClock(Start);
        var source = new FakeSource(() => Build.Ok(Start, Build.Measured(Start.AddMinutes(45))));
        var store = new FakeStore();
        var runner = new SourceIngestRunner(source, clock);

        Assert.Equal(IngestOutcome.Succeeded, await runner.RunOnceAsync(store, CancellationToken.None));

        var saved = Assert.IsType<SourceFetchResult>(store.LastSaved);

        // Stanica je i dalje tu — samo bez podatka. Da je izbačena, u UI-u bi izgledala
        // kao da ne postoji, umjesto kao da o njoj ne znamo ništa.
        var reading = Assert.IsType<StationReading.NoData>(Assert.Single(saved.Readings));
        Assert.Equal(AlertLevel.Unknown, reading.Level);
        Assert.Contains("budućnosti", reading.Reason, StringComparison.Ordinal);
        Assert.Single(saved.Skipped);
    }

    [Fact]
    public async Task Status_razdvaja_zadnji_pokusaj_od_zadnjeg_uspjeha()
    {
        var clock = new TestClock(Start);
        var attempt = 0;
        var source = new FakeSource(() =>
            ++attempt == 1 ? Build.Ok(Start, Build.Measured(Start)) : Build.Down(Start))
        {
            MinimumFetchInterval = TimeSpan.Zero,
        };

        var store = new FakeStore();
        var runner = new SourceIngestRunner(source, clock);

        await runner.RunOnceAsync(store, CancellationToken.None);
        clock.Advance(TimeSpan.FromHours(6));
        await runner.RunOnceAsync(store, CancellationToken.None);

        // Izvor koji se pokušava a ne uspijeva od jutros nije isto što i svjež izvor.
        Assert.Equal(Start, runner.Status.LastSuccessAt);
        Assert.Equal(Start.AddHours(6), runner.Status.LastAttemptAt);
        Assert.False(runner.Status.IsHealthy);
    }
}
