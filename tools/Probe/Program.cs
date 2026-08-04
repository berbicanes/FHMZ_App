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

try
{
    if (options.Includes(ProbeTargets.ArcGisSourceId))
    {
        Console.WriteLine("── ArcGIS: AVP Sava ──");
        await crawler.CrawlAsync(options.DrillAll, options.SampleAll, cts.Token);
        Console.WriteLine();
    }

    var htmlTargets = ProbeTargets.Html.Where(t => options.Includes(t.SourceId)).ToList();
    if (htmlTargets.Count > 0)
    {
        Console.WriteLine("── HTML izvori ──");
        foreach (var target in htmlTargets)
        {
            await client.FetchAsync(target.SourceId, target.Name, target.Url, "html", cts.Token);
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
        IReadOnlyList<string> Only)
    {
        public bool Includes(string sourceId) =>
            Only.Count == 0 || Only.Contains(sourceId, StringComparer.OrdinalIgnoreCase);

        public static ProbeOptions? Parse(string[] args)
        {
            var fixtureRoot = DefaultFixtureRoot();
            var delay = TimeSpan.FromMilliseconds(750);
            var drillAll = false;
            var sampleAll = false;
            var only = new List<string>();

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

            return new ProbeOptions(fixtureRoot, delay, drillAll, sampleAll, only);
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
                """);
        }
    }
}
