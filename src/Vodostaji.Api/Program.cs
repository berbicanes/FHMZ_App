using System.Text.Json.Serialization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Vodostaji.Api;
using Vodostaji.Core;
using Vodostaji.Data;
using Vodostaji.Ingest;
using Vodostaji.Ingest.AvpSava;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

var connection = builder.Configuration.GetConnectionString("Vodostaji")
    ?? Environment.GetEnvironmentVariable("VODOSTAJI_CONNECTION")
    ?? DesignTimeDbContextFactory.LocalDevelopmentConnection;

builder.Services.AddDbContext<VodostajiDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddScoped<IReadingStore, EfReadingStore>();
builder.Services.AddSingleton(TimeProvider.System);

// Jedan HttpClient po izvoru, sa User-Agentom koji nosi kontakt — LEGAL.md §2.6.
// Ime i adresa stoje na jednom mjestu, kao i u `tools/Probe`.
builder.Services.AddHttpClient<AvpSavaArcGisSource>(ConfigureSourceClient);
builder.Services.AddHttpClient<AvpSavaGeometrySource>(ConfigureSourceClient);

builder.Services.AddSingleton<SourceIngestRunner>(services => new SourceIngestRunner(
    services.GetRequiredService<AvpSavaArcGisSource>(),
    services.GetRequiredService<TimeProvider>()));

builder.Services.AddSingleton(services => new ReachMapFile(
    Path.Combine(builder.Environment.ContentRootPath, "data", "reaches.geojson"),
    services.GetRequiredService<ILogger<ReachMapFile>>()));

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

// Stanje izvora, vidljivo u UI-u. Ovdje se vidi razlika između "jug je prazan"
// i "jug je bez podatka", i ovdje stoji dokaz za pretpostavku o vremenskoj zoni.
app.MapGet("/api/v1/sources", (SourceIngestRunner runner) => Results.Ok(new[] { runner.Status }))
   .Produces<SourceStatus[]>()
   .WithName("GetSources");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static void ConfigureSourceClient(HttpClient client)
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(Contact.UserAgent);
}

/// <summary>Vidljivo testovima.</summary>
public partial class Program;
