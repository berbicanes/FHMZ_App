using Vodostaji.Core;

namespace Vodostaji.Ingest.Avpjm;

/// <summary>
/// Adapter za AVP Jadranskog mora.
///
/// Drugi izvor u sistemu i prva provjera da li model podnosi nejednakost. AVP Sava mjeri na
/// sat i objavljuje stupanj opasnosti; AVPJM mjeri svakih 15 minuta i **ne objavljuje stupanj
/// uopšte**. Adapteri se ne poznaju i ne dijele ništa osim `IStationDataSource`.
///
/// Ako AVPJM ikad odobri pristup svom ISV-u (`isvportal.jadran.ba`, isti Esri stack), ovaj
/// adapter se briše i zamjenjuje ArcGIS varijantom — interfejs ostaje isti (SOURCES.md §2).
/// </summary>
public sealed class AvpjmSource(HttpClient httpClient, TimeProvider? timeProvider = null)
    : IStationDataSource
{
    public const string Id = "avpjm";

    /// <summary>Jedan zahtjev daje cijeli sliv — svih 20 stanica sa vrijednostima i pragovima.</summary>
    public const string ListUrl = "https://avpjm.jadran.ba/vodomjerne_stanice";

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public string SourceId => Id;

    public Attribution Attribution { get; } = new()
    {
        AgencyName = "Agencija za vodno područje Jadranskog mora",
        AgencyUrl = new Uri("https://avpjm.jadran.ba"),
        SourceUrl = new Uri(ListUrl),
    };

    /// <summary>
    /// **Fiksno zimsko vrijeme cijele godine**, bez ljetnog pomaka. Suprotno od FHMZBIH-a,
    /// koji poštuje DST — dvije agencije u istoj zemlji, dvije konvencije.
    /// </summary>
    public SourceClock Clock { get; } = new()
    {
        Convention = ClockConvention.FixedOffset,
        FixedOffset = TimeSpan.FromHours(1),
        Evidence =
            "Dokazano 2026-08-04. Polje `owner` doslovno kaže 'zimsko računanje vremena'. "
            + "Snimak u 22:33Z nosio je zadnje očitanje sa oznakom 23:15, što je kao UTC "
            + "42 minute u budućnosti; kao CET ispada 22:15Z, tj. 18 minuta prije dohvata "
            + "uz korak od 15 minuta. Offset ostaje +1 i ljeti i zimi.",
    };

    /// <summary>Kadenca je 15 minuta; tražimo 15 kao i LEGAL.md §2.5 minimum.</summary>
    public TimeSpan MinimumFetchInterval => TimeSpan.FromMinutes(15);

    /// <summary>Korak serije je 900 sekundi, mjereno na 2976 očitanja (SOURCES.md §2).</summary>
    public static TimeSpan ExpectedInterval => TimeSpan.FromMinutes(15);

    /// <summary>
    /// Mjereno 2026-08-04: zadnje očitanje bilo je 18 minuta staro u trenutku dohvata.
    /// Uzeta je granica od 30 minuta, jer je i to unutar dva koraka serije.
    /// </summary>
    public static TimeSpan TypicalPublicationLag => TimeSpan.FromMinutes(30);

    public async Task<SourceFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        var fetchedAt = _time.GetUtcNow();

        try
        {
            var html = await httpClient.GetStringAsync(ListUrl, cancellationToken).ConfigureAwait(false);

            var parsed = AvpjmListParser.Parse(
                html, Clock, Attribution, ExpectedInterval, TypicalPublicationLag);

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
            // Pad ovog izvora ne smije dodirnuti AVP Savu (zlatno pravilo 5).
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
