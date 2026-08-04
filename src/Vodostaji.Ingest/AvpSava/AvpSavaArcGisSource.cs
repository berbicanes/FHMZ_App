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
    /// `DATE_TIME` je pravi UTC epoch — dokazano 2026-08-04, vidi <see cref="SourceClock.Evidence"/>.
    ///
    /// Ovo je ispravka ranije pretpostavke. Do dokaza se čitalo pesimistično kao CEST, što je
    /// podatke prikazivalo dva sata starijim nego što jesu. Pesimizam je bio ispravan izbor
    /// dok se nije znalo, ali nije zamjena za provjeru.
    /// </summary>
    public SourceClock Clock { get; } = new()
    {
        Convention = ClockConvention.Utc,
        Evidence =
            "Dokazano 2026-08-04. Sloj deklariše `dateFieldsTimeReference` = Central European "
            + "Standard Time, `respectsDaylightSaving` = true, `datesInUnknownTimezone` = false. "
            + "Provjereno upitom: `DATE_TIME = '2026-08-05 00:00:00'` vraća 28 zapisa, dok "
            + "`= '2026-08-04 22:00:00'` vraća 0. Baza dakle drži lokalno zidno vrijeme, a "
            + "servis vraća epoch 1785880800000 = 2026-08-04 22:00Z, što je tačno taj trenutak "
            + "u UTC-u. Konverziju radi servis, pa je epoch već UTC i ne dira se.",
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
