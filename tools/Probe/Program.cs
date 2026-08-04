using Vodostaji.Probe;

var options = ProbeOptions.Parse(args);
if (options is null)
{
    ProbeOptions.PrintUsage();
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine();
    Console.WriteLine("Prekinuto — snimljeni fixtures ostaju.");
};

var runStamp = DateTimeOffset.UtcNow;

Console.WriteLine($"Probe — Faza 0 · {runStamp:yyyy-MM-dd HH:mm:ss}Z");
Console.WriteLine($"User-Agent: {Contact.UserAgent}");
Console.WriteLine($"Fixtures:   {options.FixtureRoot}");
Console.WriteLine($"Pauza:      {options.Delay.TotalMilliseconds:N0}ms između zahtjeva");
Console.WriteLine();

using var client = new ProbeClient(options.FixtureRoot, options.Delay, runStamp);
var crawler = new ArcGisCrawler(client, ProbeTargets.ArcGisSourceId, ProbeTargets.ArcGisRoot);

if (options.WatchInterval is { } interval)
{
    var watcher = new Watcher(client, options.FixtureRoot);
    var csv = Path.Combine(options.FixtureRoot, "_watch", "watch.csv");

    Console.WriteLine($"Watch mod — ciklus svakih {interval.TotalMinutes:N0} min, {options.WatchCycles} ciklusa");
    Console.WriteLine($"CSV: {csv}");
    Console.WriteLine("Mjeri se pomak između vremena koje izvor tvrdi i stvarnog vremena.");
    Console.WriteLine();

    for (var cycle = 1; cycle <= options.WatchCycles && !cts.IsCancellationRequested; cycle++)
    {
        var now = DateTimeOffset.UtcNow;
        Console.WriteLine($"── ciklus {cycle}/{options.WatchCycles} · {now:yyyy-MM-dd HH:mm}Z ──");

        try
        {
            var count = await watcher.RunCycleAsync(now, cts.Token);
            Console.WriteLine($"   {count} očitanja upisano");
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            // Pad jednog ciklusa ne smije prekinuti mjerenje koje traje 24 sata.
            Console.WriteLine($"   ciklus pao: {ex.Message}");
        }

        if (cycle == options.WatchCycles)
        {
            break;
        }

        try
        {
            await Task.Delay(interval, cts.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Gotovo. Analiziraj: {csv}");
    return 0;
}

try
{
    if (options.Includes(ProbeTargets.ArcGisSourceId))
    {
        Console.WriteLine("── ArcGIS: AVP Sava ──");
        await crawler.CrawlAsync(options.DrillAll, options.SampleAll, cts.Token);
        Console.WriteLine();
    }

    var pageTargets = ProbeTargets.Pages.Where(t => options.Includes(t.SourceId)).ToList();
    if (pageTargets.Count > 0)
    {
        Console.WriteLine("── Ostali izvori ──");
        foreach (var target in pageTargets)
        {
            await client.FetchAsync(target.SourceId, target.Name, target.Url, target.Extension, cts.Token);
        }
        Console.WriteLine();
    }
}
catch (OperationCanceledException)
{
    // Izvještaj se svejedno piše — ono što je snimljeno je snimljeno.
}

var reportPath = await SchemaReport.WriteAsync(
    options.FixtureRoot, runStamp, client.Results, crawler.Discovered, CancellationToken.None);

var failed = client.Results.Count(r => !r.Ok);

Console.WriteLine($"Zahtjeva: {client.Results.Count} · neuspjelih: {failed}");
Console.WriteLine($"Servisa otkriveno: {crawler.Discovered.Count}");
Console.WriteLine($"Izvještaj: {reportPath}");

if (failed > 0)
{
    Console.WriteLine();
    Console.WriteLine("Neuspjeli zahtjevi — svaki je rupa u verifikaciji, ne detalj:");
    foreach (var r in client.Results.Where(r => !r.Ok))
    {
        Console.WriteLine($"  {r.SourceId}/{r.Name}: {r.Error}");
    }
}

return failed > 0 ? 2 : 0;

namespace Vodostaji.Probe
{
    internal sealed record ProbeOptions(
        string FixtureRoot,
        TimeSpan Delay,
        bool DrillAll,
        bool SampleAll,
        IReadOnlyList<string> Only,
        TimeSpan? WatchInterval,
        int WatchCycles)
    {
        /// <summary>
        /// LEGAL.md §2.5 i SOURCES.md obećavaju izvorima najmanje 10 minuta između pogodaka.
        /// Obećanje koje alat može prekršiti nije obećanje, pa ga alat odbija prekršiti.
        /// </summary>
        public static readonly TimeSpan MinWatchInterval = TimeSpan.FromMinutes(10);


        public bool Includes(string sourceId) =>
            Only.Count == 0 || Only.Contains(sourceId, StringComparer.OrdinalIgnoreCase);

        public static ProbeOptions? Parse(string[] args)
        {
            var fixtureRoot = DefaultFixtureRoot();
            var delay = TimeSpan.FromMilliseconds(750);
            var drillAll = false;
            var sampleAll = false;
            var only = new List<string>();
            TimeSpan? watchInterval = null;
            var watchCycles = 24;

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--out" when i + 1 < args.Length:
                        fixtureRoot = Path.GetFullPath(args[++i]);
                        break;

                    case "--delay" when i + 1 < args.Length:
                        if (!int.TryParse(args[++i], out var ms) || ms < 0)
                        {
                            return null;
                        }
                        delay = TimeSpan.FromMilliseconds(ms);
                        break;

                    case "--only" when i + 1 < args.Length:
                        only.AddRange(args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries));
                        break;

                    case "--watch" when i + 1 < args.Length:
                        if (!int.TryParse(args[++i], out var minutes))
                        {
                            return null;
                        }

                        watchInterval = TimeSpan.FromMinutes(minutes);
                        if (watchInterval < MinWatchInterval)
                        {
                            Console.Error.WriteLine(
                                $"--watch mora biti najmanje {MinWatchInterval.TotalMinutes:N0} minuta " +
                                "(rate limit obećan u LEGAL.md §2.5).");
                            return null;
                        }

                        break;

                    case "--cycles" when i + 1 < args.Length:
                        if (!int.TryParse(args[++i], out watchCycles) || watchCycles < 1)
                        {
                            return null;
                        }
                        break;

                    case "--drill-all":
                        drillAll = true;
                        break;

                    case "--sample-all":
                        sampleAll = true;
                        break;

                    default:
                        return null;
                }
            }

            return new ProbeOptions(
                fixtureRoot, delay, drillAll, sampleAll, only, watchInterval, watchCycles);
        }

        /// <summary>Fixtures idu u repo, ne pored binarija — traži korijen po `.git`.</summary>
        private static string DefaultFixtureRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                dir = dir.Parent;
            }

            var root = dir?.FullName ?? Directory.GetCurrentDirectory();
            return Path.Combine(root, "tests", "fixtures");
        }

        public static void PrintUsage()
        {
            Console.WriteLine("""
                Probe — verifikacija izvora (Faza 0)

                  dotnet run --project tools/Probe [opcije]

                  --out <dir>     korijen za fixtures (podrazumijevano tests/fixtures)
                  --delay <ms>    pauza između zahtjeva (podrazumijevano 750)
                  --only <ids>    ograniči na izvore, zarezom odvojeno (avp-sava,avpjm,fhmzbih)
                  --drill-all     buši u svaki otkriveni servis, ne samo one iz SOURCES.md
                  --sample-all    povuci uzorak podataka za svaki sloj, ne samo imenovane

                Watch mod — mjeri pomak vremenskih zona kroz vrijeme:

                  --watch <min>   ciklus svakih N minuta (najmanje 10)
                  --cycles <n>    koliko ciklusa (podrazumijevano 24)

                  dotnet run --project tools/Probe -- --watch 60 --cycles 24
                """);
        }
    }
}
