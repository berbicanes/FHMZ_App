using System.Text.Json.Serialization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using Serilog;
using Vodostaji.Api;
using Vodostaji.Core;
using Vodostaji.Data;
using Vodostaji.Ingest;
using Vodostaji.Ingest.AvpSava;
using Vodostaji.Ingest.Avpjm;
using Vodostaji.Ingest.Fhmzbih;
using Vodostaji.Ingest.Wiski;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Redoslijed je bitan i bio je pogrešan.
//
// `appsettings.json` se pakuje u sliku i nosi razvojni connection string. Dok je stajao
// prvi, gazio je varijablu okruženja — kontejner se uporno spajao na `localhost` i padao,
// a razlog se vidio tek u logu. Okruženje sada pobjeđuje, jer je ono jedino što se pri
// deployu stvarno mijenja.
//
// `ConnectionStrings__Vodostaji` i dalje radi kroz standardnu ASP.NET konfiguraciju.
var connection = Environment.GetEnvironmentVariable("VODOSTAJI_CONNECTION")
    ?? builder.Configuration.GetConnectionString("Vodostaji")
    ?? DesignTimeDbContextFactory.LocalDevelopmentConnection;

builder.Services.AddDbContext<VodostajiDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddScoped<IReadingStore, EfReadingStore>();
builder.Services.AddScoped<EfHistoryReader>();
builder.Services.AddScoped<EfPreviousReadingReader>();
builder.Services.AddScoped<EfObservedIntervalReader>();
builder.Services.AddSingleton(TimeProvider.System);

// Jedan HttpClient po izvoru, sa User-Agentom koji nosi kontakt — LEGAL.md §2.6.
// Ime i adresa stoje na jednom mjestu, kao i u `tools/Probe`.
builder.Services.AddHttpClient<AvpSavaArcGisSource>(ConfigureSourceClient);
builder.Services.AddHttpClient<AvpSavaGeometrySource>(ConfigureSourceClient);
builder.Services.AddHttpClient<AvpSavaStationSource>(ConfigureSourceClient);
builder.Services.AddHttpClient<AvpjmSource>(ConfigureSourceClient);
builder.Services.AddHttpClient<FhmzbihSource>(ConfigureSourceClient);
builder.Services.AddHttpClient<WiskiSource>(ConfigureSourceClient);

// Jedan pipeline po izvoru. Dodavanje trećeg izvora je dodavanje jedne registracije;
// ne postoji mjesto na kojem bi se dva izvora mogla nehotice preplesti.
builder.Services.AddSingleton<SourcePipeline>(services => new AvpSavaPipeline(
    new SourceIngestRunner(
        services.GetRequiredService<AvpSavaArcGisSource>(),
        services.GetRequiredService<TimeProvider>()),
    services.GetRequiredService<AvpSavaGeometrySource>(),
    services.GetRequiredService<AvpSavaStationSource>(),
    services.GetRequiredService<ReachMapFile>(),
    services.GetRequiredService<StationMapFile>(),
    services.GetRequiredService<TimeProvider>(),
    services.GetRequiredService<ILogger<AvpSavaPipeline>>()));

builder.Services.AddSingleton<SourcePipeline>(services => new PointSourcePipeline(
    new SourceIngestRunner(
        services.GetRequiredService<AvpjmSource>(),
        services.GetRequiredService<TimeProvider>()),
    new AvpjmLegend(),
    PointMapFileFor(services, AvpjmSource.Id),
    services.GetRequiredService<TimeProvider>()));

builder.Services.AddSingleton<SourcePipeline>(services => new PointSourcePipeline(
    new SourceIngestRunner(
        services.GetRequiredService<FhmzbihSource>(),
        services.GetRequiredService<TimeProvider>()),
    new FhmzbihLegend(),
    PointMapFileFor(services, FhmzbihSource.Id),
    services.GetRequiredService<TimeProvider>()));

// Četvrti izvor: WISKI izvoz iste agencije kao `avp-sava`, ali drugi sistem i drugi podaci
// — 98 tačaka sa vodostajem, proticajem i temperaturom vode, bez ijedne ocjene opasnosti.
// Zaseban pipeline i zasebna legenda; slojevi se ne stapaju (SOURCES.md §4.5).
builder.Services.AddSingleton<SourcePipeline>(services => new PointSourcePipeline(
    new SourceIngestRunner(
        services.GetRequiredService<WiskiSource>(),
        services.GetRequiredService<TimeProvider>()),
    new WiskiLegend(),
    PointMapFileFor(services, WiskiSource.Id),
    services.GetRequiredService<TimeProvider>()));

builder.Services.AddSingleton(services => new ReachMapFile(
    Path.Combine(builder.Environment.ContentRootPath, "data", "reaches.geojson"),
    services.GetRequiredService<ILogger<ReachMapFile>>()));

// Registar stanica je zaseban fajl jer je i zaseban sloj. Dionice i stanice se ne spajaju
// (SOURCES.md §1.7), pa se ne spajaju ni u jedan odgovor.
builder.Services.AddSingleton(services => new StationMapFile(
    Path.Combine(builder.Environment.ContentRootPath, "data", "stations.geojson"),
    services.GetRequiredService<ILogger<StationMapFile>>()));

// Jedan fajl po tačkastom izvoru. Slojevi se ne stapaju ni na disku.
builder.Services.AddSingleton<IReadOnlyDictionary<string, PointMapFile>>(services =>
    new[] { AvpjmSource.Id, FhmzbihSource.Id, WiskiSource.Id }.ToDictionary(
        id => id,
        id => new PointMapFile(
            id,
            Path.Combine(builder.Environment.ContentRootPath, "data", $"{id}.geojson"),
            services.GetRequiredService<ILogger<PointMapFile>>()),
        StringComparer.Ordinal));

builder.Services.AddHostedService<IngestHostedService>();

// OpenAPI shema postoji da bi se TypeScript tipovi **generisali**, ne pisali ručno
// (CLAUDE.md → Konvencije). `npm run generate:api` u src/Vodostaji.Web je čita odavde.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "Vodostaji BiH",
    Version = "v1",
    Description = "Stanje rijeka u BiH, objedinjeno iz javnih izvora više agencija. "
                + "Podaci nisu zvanični i ne služe za odbranu od poplava.",
}));

// Enum-i kao tekst. `"circuit": 0` u API-ju ne znači ništa nikome ko ne gleda izvorni kod,
// a `"circuit": "Closed"` znači svima.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Kompresija je uključena i za HTTPS. GeoJSON dionica se sa 239 KB spusti na oko 72 KB,
// a mapa se otvara i na slaboj vezi.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = [.. ResponseCompressionDefaults.MimeTypes, "application/geo+json"];
});

var app = builder.Build();

app.UseSwagger();

app.UseResponseCompression();
app.UseSerilogRequestLogging();

// Mapa koju job prepisuje. Keš je kratak i namjerno kraći od kadence izvora —
// kešovati agresivnije nego što se izvor osvježava bila bi lažna svježina.
app.MapGet("/api/v1/geojson/reaches", async (ReachMapFile file, CancellationToken ct) =>
{
    var geoJson = await file.ReadAsync(ct);

    return geoJson is null
        ? Results.Problem(
            "Mapa još nije generisana. Ingest nije uspio nijednom od pokretanja.",
            statusCode: StatusCodes.Status503ServiceUnavailable)
        : Results.Text(geoJson, "application/geo+json");
})
   // Tijelo je već serijalizovan GeoJSON, ali shema mora u OpenAPI da bi se TS tipovi
   // generisali iz istog izvora iz kojeg backend gradi odgovor.
   .Produces<ReachFeatureCollection>(StatusCodes.Status200OK, "application/geo+json")
   .WithName("GetReaches");

// Tačkasti izvori — svaki svoj sloj sa svojom legendom. Nikad stopljeni sa dionicama
// ni međusobno.
app.MapGet("/api/v1/geojson/points/{sourceId}", async (
    string sourceId,
    IReadOnlyDictionary<string, PointMapFile> files,
    CancellationToken ct) =>
{
    if (!files.TryGetValue(sourceId, out var file))
    {
        return Results.NotFound(new { message = $"Izvor `{sourceId}` nije tačkasti sloj." });
    }

    var geoJson = await file.ReadAsync(ct);

    return geoJson is null
        ? Results.Problem(
            $"Sloj `{sourceId}` još nije generisan.",
            statusCode: StatusCodes.Status503ServiceUnavailable)
        : Results.Text(geoJson, "application/geo+json");
})
   .Produces<ReachFeatureCollection>(StatusCodes.Status200OK, "application/geo+json")
   .WithName("GetPointSource");

// Registar mjernih mjesta. Nema status ni boju — kaže gdje se mjeri, ne kakvo je stanje.
app.MapGet("/api/v1/geojson/stations", async (StationMapFile file, CancellationToken ct) =>
{
    var geoJson = await file.ReadAsync(ct);

    return geoJson is null
        ? Results.Problem(
            "Registar stanica još nije povučen.",
            statusCode: StatusCodes.Status503ServiceUnavailable)
        : Results.Text(geoJson, "application/geo+json");
})
   .Produces<StationFeatureCollection>(StatusCodes.Status200OK, "application/geo+json")
   .WithName("GetStations");

// Historija jedne dionice, za graf 7/30 dana (UI.md §3).
// Izvor je **dio putanje**, ne pretpostavka.
//
// Ranije je ovdje stajalo `AvpSavaArcGisSource.Id` zakucano. Ključ `28` postoji i kod AVP
// Save i kod AVPJM-a, pa je otvaranje stanice Malo Polje 2 povlačilo historiju dionice AVP
// Save — graf jedne rijeke pod imenom druge. Domenski model cijelo vrijeme kaže da ključ
// vrijedi samo unutar izvora; ovaj endpoint ga nije slušao.
app.MapGet("/api/v1/reaches/{sourceId}/{stationKey}/history", async (
    string sourceId,
    string stationKey,
    int? days,
    EfHistoryReader reader,
    TimeProvider time,
    CancellationToken ct) =>
{
    // Samo 7 i 30 dana. Proizvoljan broj bi bio API koji obećava rezolucije koje nemamo.
    var window = days == 30 ? 30 : 7;

    var history = await reader.ReadAsync(sourceId, stationKey, window, time.GetUtcNow(), ct);

    return history is null
        ? Results.NotFound(new
            {
                message = $"Dionica `{stationKey}` ne postoji kod izvora `{sourceId}`.",
            })
        : Results.Ok(history);
})
   .Produces<ReachHistory>()
   .WithName("GetReachHistory");

// Stanje izvora, vidljivo u UI-u. Ovdje se vidi razlika između "jug je prazan"
// i "jug je bez podatka", i ovdje stoji dokaz za pretpostavku o vremenskoj zoni.
// Stanje svih izvora. UI iz ovoga razlikuje "jug je prazan" od "jug je bez podatka".
app.MapGet("/api/v1/sources", (IEnumerable<SourcePipeline> pipelines) =>
        Results.Ok(pipelines.Select(p => p.Runner.Status).ToArray()))
   .Produces<SourceStatus[]>()
   .WithName("GetSources");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Sagrađeni SPA, kad postoji.
//
// `/dionica/{key}` i `/stanica/{key}` su rute browsera, ne servera — bez fallbacka na
// `index.html` podijeljen link daje 404, a deep linkovi su po UI.md §4 glavni kanal
// distribucije. U razvoju ovo radi Vite, pa se blok preskače ako builda nema.
var webRoot = builder.Configuration["WebRoot"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "Vodostaji.Web", "dist");

if (Directory.Exists(webRoot))
{
    var files = new PhysicalFileProvider(Path.GetFullPath(webRoot));

    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });

    // Fallback ide **samo** na rute koje nisu API. `/api/...` koji ne postoji mora ostati
    // 404, jer bi `index.html` kao odgovor na API poziv izgledao kao uspjeh sa čudnim tijelom.
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(files.GetFileInfo("index.html"));
    });

    app.Logger.LogInformation("SPA se servira iz {WebRoot}.", Path.GetFullPath(webRoot));
}
else
{
    app.Logger.LogInformation(
        "SPA build nije nađen u {WebRoot}; u razvoju ga servira Vite.", webRoot);
}

app.Run();

static PointMapFile PointMapFileFor(IServiceProvider services, string sourceId) =>
    services.GetRequiredService<IReadOnlyDictionary<string, PointMapFile>>()[sourceId];

static void ConfigureSourceClient(HttpClient client)
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(Contact.UserAgent);
}

/// <summary>Vidljivo testovima.</summary>
public partial class Program;
