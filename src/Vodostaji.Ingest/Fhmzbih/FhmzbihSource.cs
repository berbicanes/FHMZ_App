using Vodostaji.Core;

namespace Vodostaji.Ingest.Fhmzbih;

/// <summary>
/// Adapter za FHMZBIH — treći izvor, treća konvencija.
///
/// AVP Sava objavljuje stupanj opasnosti i mjeri na sat. AVPJM ne objavljuje stupanj i drži
/// zimsko vrijeme cijele godine. FHMZBIH ne objavljuje stupanj, **poštuje ljetno vrijeme**,
/// i jedini od njih **objavljuje trend**.
///
/// Uloga: cross-check za sliv Save i pokrivač rupa (SOURCES.md §3).
/// </summary>
public sealed class FhmzbihSource(HttpClient httpClient, TimeProvider? timeProvider = null)
    : IStationDataSource
{
    public const string Id = "fhmzbih";

    public const string IndexUrl = "https://www.fhmzbih.gov.ba/latinica/HIDRO/";

    /// <summary>
    /// Podstranice stanica, sa koordinatama i kotom nule. Imena fajlova nisu izvedena iz
    /// naziva stanica — `Sanski Most` je `hvsSMost`, `Han Bila` je `hvsHBila` — pa se ne
    /// pogađaju nego stoje prepisana sa njihove stranice (SOURCES.md §3).
    /// </summary>
    private static readonly (string Name, string Page)[] StationPages =
    [
        ("Bihać", "hvsBihac"),
        ("Martin Brod", "hvsMBrod"),
        ("Sanski Most", "hvsSMost"),
        ("Vrhpolje", "hvsVrhpolje"),
        ("Sarajevo", "hvsCumurija"),
        ("Reljevo", "hvsReljevo"),
        ("Zenica", "hvsZenica"),
        // Njihov pregled piše `Kiseljk`, podstranica se zove `hvsKiseljak`. Tipfeler je
        // njihov i vjerovatno će ga jednom popraviti, pa se traži i jedno i drugo ime.
        ("Kiseljk", "hvsKiseljak"),
        ("Kiseljak", "hvsKiseljak"),
        ("Han Bila", "hvsHBila"),
        ("Tuzla", "hvsTuzla"),
        ("Kašići", "hvsKasici"),
        ("Konjic", "hvsKonjic"),
    ];

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private Dictionary<string, FhmzbihStationDetails> _details = [];
    private DateTimeOffset? _detailsFetchedAt;

    public string SourceId => Id;

    public Attribution Attribution { get; } = new()
    {
        AgencyName = "Federalni hidrometeorološki zavod BiH",
        AgencyUrl = new Uri("https://www.fhmzbih.gov.ba"),
        SourceUrl = new Uri(IndexUrl),
    };

    /// <summary>
    /// Lokalno vrijeme **sa punim DST pravilima**, suprotno od AVPJM-a.
    /// Dvije agencije u istoj zemlji, dvije konvencije — ista funkcija za obje bila bi greška.
    /// </summary>
    public SourceClock Clock { get; } = new()
    {
        Convention = ClockConvention.LocalWithDst,
        TimeZoneId = "Europe/Sarajevo",
        Evidence =
            "Dokazano 2026-08-04. Martin Brod je nosio `5.8.2026 00:00` u trenutku 22:43Z. "
            + "Kao CEST to je 22:00Z, tj. 43 minute prije očitavanja; kao CET bi bilo 23:00Z, "
            + "što je 17 minuta u budućnosti i nemoguće.",
    };

    public TimeSpan MinimumFetchInterval => TimeSpan.FromMinutes(15);

    /// <summary>
    /// Aktivne stanice se javljaju na sat. **Ne sve** — u snimku od 2026-08-04 Bihać je
    /// nosio oznaku od prije 16 sati dok je Martin Brod bio svjež. `ExpectedInterval` po
    /// stanici traži mjerenje kroz nekoliko dana; do tada stoji sat, pa stanice koje kasne
    /// pošteno ispadaju zastarjele.
    /// </summary>
    public static TimeSpan ExpectedInterval => TimeSpan.FromHours(1);

    public static TimeSpan TypicalPublicationLag => TimeSpan.FromMinutes(60);

    /// <summary>Registar se mijenja rijetko; dvanaest zahtjeva jednom dnevno.</summary>
    public static TimeSpan DetailsRefreshInterval => TimeSpan.FromHours(24);

    public async Task<SourceFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        var fetchedAt = _time.GetUtcNow();

        try
        {
            await EnsureDetailsAsync(fetchedAt, cancellationToken).ConfigureAwait(false);

            var html = await httpClient.GetStringAsync(IndexUrl, cancellationToken)
                .ConfigureAwait(false);

            var parsed = FhmzbihParser.ParseIndex(
                html, Clock, Attribution, ExpectedInterval, TypicalPublicationLag, _details);

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
            return new SourceFetchResult
            {
                SourceId = Id,
                FetchedAt = fetchedAt,
                Readings = [],
                FailureReason = ex.Message,
            };
        }
    }

    private async Task EnsureDetailsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (_detailsFetchedAt is { } fetched &&
            now - fetched < DetailsRefreshInterval &&
            _details.Count > 0)
        {
            return;
        }

        var collected = new Dictionary<string, FhmzbihStationDetails>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, page) in StationPages)
        {
            try
            {
                var html = await httpClient
                    .GetStringAsync($"{IndexUrl}{page}.php", cancellationToken)
                    .ConfigureAwait(false);

                if (FhmzbihParser.ParseStationPage(html, name) is { } details)
                {
                    collected[name] = details;
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Jedna nedostupna podstranica znači jednu stanicu bez koordinata, ne pad
                // cijelog izvora. Vrijednosti i dalje dolaze sa glavne stranice.
            }
        }

        if (collected.Count > 0)
        {
            _details = collected;
            _detailsFetchedAt = now;
        }
    }
}
