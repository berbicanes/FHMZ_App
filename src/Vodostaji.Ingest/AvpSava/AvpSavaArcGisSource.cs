using Vodostaji.Core;

namespace Vodostaji.Ingest.AvpSava;

/// <summary>
/// Adapter za AVP Savu. Zna samo za svoj izvor — ne za bazu, ne za UI, ne za druge adaptere.
/// Sav prevod je u <see cref="AvpSavaReachParser"/>, pa je ovdje ostalo samo dohvatanje.
/// </summary>
public sealed class AvpSavaArcGisSource(HttpClient httpClient, TimeProvider? timeProvider = null)
    : IStationDataSource
{
    public const string Id = "avp-sava";

    /// <summary>
    /// SOURCES.md §1.1: `f=geojson` je podržan na ovom sloju i preferiran.
    /// Geometrija se ovdje ne traži — poligoni dionica idu zasebnim putem u mapu, a ingest
    /// job nosi samo vrijednosti. Manji odgovor je i manje opterećenje za njihov server.
    /// </summary>
    public const string QueryUrl =
        "https://isvportal.voda.ba/server/rest/services/Hidrolosko_stane_u_realnom_vremenu/" +
        "FeatureServer/0/query?where=1%3D1&outFields=*&outSR=4326&returnGeometry=false&f=json";

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public string SourceId => Id;

    public Attribution Attribution { get; } = new()
    {
        AgencyName = "Agencija za vodno područje rijeke Save",
        AgencyUrl = new Uri("https://www.voda.ba"),
        SourceUrl = new Uri(
            "https://isvportal.voda.ba/server/rest/services/" +
            "Hidrolosko_stane_u_realnom_vremenu/FeatureServer/0"),
    };

    /// <summary>
    /// Zona `DATE_TIME`-a **nije dokazana** (SOURCES.md → Otvorena pitanja). Do dokaza se
    /// čita pesimistično, kao ljetno lokalno vrijeme, pa podatak ispada najstariji koji bi
    /// mogao biti. Kad mjerenje iz `tests/fixtures/_watch/` da odgovor, mijenja se ovdje —
    /// na jednom mjestu, sa novim dokazom u <see cref="SourceClock.Evidence"/>.
    /// </summary>
    public SourceClock Clock { get; } = new()
    {
        Convention = ClockConvention.Unverified,
        PessimisticOffset = TimeSpan.FromHours(2),
        Evidence =
            "Neriješeno na dan 2026-08-04. Snimak u 22:25Z pokazuje najsvježiji timestamp "
            + "21:00Z, bez ijedne vrijednosti u budućnosti, pa se UTC/CET/CEST ne razlikuju "
            + "iz samog podatka. Čita se kao CEST jer to daje najstariji mogući trenutak.",
    };

    /// <summary>
    /// Izvor se osvježava na sat (mjereno kroz `--watch`). LEGAL.md §2.5 obećava najmanje
    /// 10 minuta; ovdje tražimo 15, jer češće od toga ionako nema šta novo da stigne.
    /// </summary>
    public TimeSpan MinimumFetchInterval => TimeSpan.FromMinutes(15);

    /// <summary>Sat je kadenca izvora, ne naša pretpostavka o svježini.</summary>
    public static TimeSpan ExpectedInterval => TimeSpan.FromHours(1);

    public async Task<SourceFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        var fetchedAt = _time.GetUtcNow();

        try
        {
            using var response = await httpClient
                .GetAsync(QueryUrl, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var body = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            var parsed = AvpSavaReachParser.Parse(body, Clock, Attribution, ExpectedInterval);

            return new SourceFetchResult
            {
                SourceId = Id,
                FetchedAt = fetchedAt,
                Readings = parsed.Readings,
                Skipped = parsed.Skipped,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Pad izvora je stanje koje se prijavljuje, ne izuzetak koji putuje dalje.
            // Stari podatak ostaje gdje jeste, sa svojim poštenim timestampom.
            return new SourceFetchResult
            {
                SourceId = Id,
                FetchedAt = fetchedAt,
                Readings = [],
                FailureReason = ex.Message,
            };
        }
    }
}
