using Vodostaji.Core;

namespace Vodostaji.Ingest.Wiski;

/// <summary>
/// Statički WISKI izvoz AVP Save — četvrti izvor, i prvi koji nosi **više od vodostaja**.
///
/// <para>
/// Ovo je isti vodoprivredni subjekt kao <c>avp-sava</c>, ali drugi sistem i drugi podaci,
/// pa je i zaseban izvor. ArcGIS sloj daje 45 <b>dionica</b> sa ocjenom opasnosti; ovaj izvoz
/// daje ~98 <b>tačaka</b> sa vrijednostima, bez ijedne ocjene. Spojiti ih u jedan sloj sa
/// jednom legendom je izričito zabranjeno (CLAUDE.md → Šta NE raditi), i s razlogom: jedan
/// od njih tvrdi stupanj opasnosti, drugi ne tvrdi ništa.
/// </para>
///
/// <para>
/// Tri stvari ovdje ne postoje jer ih izvoz rješava u startu (SOURCES.md §4.5): nema
/// pogađanja vremenske zone (pomak je u svakoj vrijednosti), nema izvođenja imena rijeke iz
/// naziva (dolazi iz njihove baze), i nema poligona prosječne površine 339 km² umjesto tačke.
/// </para>
/// </summary>
public sealed class WiskiSource(HttpClient httpClient, TimeProvider? timeProvider = null)
    : IStationDataSource
{
    public const string Id = "avp-sava-wiski";

    public const string BaseUrl = "https://vodostaji.voda.ba/data/internet/";

    /// <summary>
    /// Slojevi koji ulaze, i oni koji ne ulaze.
    ///
    /// <para>
    /// 80 (`EPPVodostaj`) i 90 (`EPPProticaj`) su izostavljeni: 59 od 81 odnosno 35 od 40
    /// redova nema `L1_timestamp`. Vrijednost bez vremena mjerenja se ne smije prikazati, pa
    /// bi ti slojevi donijeli uglavnom preskočene redove i lažan dojam pokrivenosti.
    /// </para>
    ///
    /// <para>
    /// 40 i 50 (podzemna voda) <b>ulaze iako su zastarjeli</b> — najsvježije očitanje im je
    /// 71 odnosno 132 dana staro. Ne prikazuju se kao trenutno stanje: svako mjerenje nosi
    /// vlastito vrijeme, pa ih prikaz starosti sam označi. Izostaviti ih značilo bi sakriti
    /// da mreža podzemnih voda postoji ali ne radi, a to je nalaz, ne šum.
    /// </para>
    /// </summary>
    public static readonly int[] Layers = [10, 20, 30, 40, 50, 60, 70];

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public string SourceId => Id;

    public Attribution Attribution { get; } = new()
    {
        AgencyName = "Agencija za vodno područje rijeke Save",
        AgencyUrl = new Uri("https://www.voda.ba"),
        SourceUrl = new Uri("https://vodostaji.voda.ba/"),
    };

    /// <summary>
    /// Jedina konvencija bez rizika: pomak stoji u svakoj vrijednosti.
    /// </summary>
    public SourceClock Clock { get; } = new()
    {
        Convention = ClockConvention.ExplicitInValue,
        Evidence =
            "Očitano 2026-08-08. `L1_timestamp` je oblika `2026-08-08T21:00:00.000+02:00` — "
            + "pomak je dio same vrijednosti, pa se trenutak ne rekonstruiše nego čita. "
            + "Nijedan problem iz SOURCES.md §1.6 i §2 se ovdje ne pojavljuje.",
    };

    /// <summary>Sedam zahtjeva po ciklusu na statičke fajlove; 15 minuta je iznad obećanih 10.</summary>
    public TimeSpan MinimumFetchInterval => TimeSpan.FromMinutes(15);

    /// <summary>Vremenske oznake su na puni sat. Ostaje pretpostavka dok se ne izmjeri.</summary>
    public static TimeSpan ExpectedInterval => TimeSpan.FromHours(1);

    /// <summary>
    /// Mjereno jednom, 2026-08-08: u 21:30 UTC je najsvježije očitanje nosilo 21:00+02:00.
    /// Granica je postavljena na sat, jer jedno mjerenje nije mjerenje kadence.
    /// </summary>
    public static TimeSpan TypicalPublicationLag => TimeSpan.FromHours(1);

    public async Task<SourceFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        var fetchedAt = _time.GetUtcNow();

        var rows = new List<WiskiRow>();
        var skipped = new List<SkippedStation>();
        var failures = new List<string>();

        foreach (var layer in Layers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var json = await httpClient
                    .GetStringAsync($"{BaseUrl}layers/{layer}/index.json", cancellationToken)
                    .ConfigureAwait(false);

                var parsed = WiskiParser.ParseLayer(json, Clock);
                rows.AddRange(parsed.Rows);
                skipped.AddRange(parsed.Skipped);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Jedan nedostupan sloj je jedan parametar manje, ne pad izvora — isto
                // pravilo koje važi među izvorima važi i unutar ovog (zlatno pravilo 5).
                failures.Add($"sloj {layer}: {ex.Message}");
            }
        }

        // Svi slojevi pali znači da izvor nije odgovorio. Prazan run i run bez podataka nisu
        // ista stvar, pa se ne smije vratiti uspjeh sa nula stanica.
        if (rows.Count == 0)
        {
            return new SourceFetchResult
            {
                SourceId = Id,
                FetchedAt = fetchedAt,
                Readings = [],
                Skipped = skipped,
                FailureReason = failures.Count > 0
                    ? string.Join("; ", failures)
                    : "Nijedan sloj nije dao nijedan red.",
            };
        }

        return new SourceFetchResult
        {
            SourceId = Id,
            FetchedAt = fetchedAt,
            Readings = Merge(rows, Attribution),
            Skipped = skipped,
            // Djelimičan neuspjeh se **ne** prijavljuje kao pad: podaci koji su stigli su
            // tačni. Ostaje u preskočenima da se vidi šta nedostaje.
            FailureReason = null,
        };
    }

    /// <summary>
    /// Spaja redove iz svih slojeva u jedno očitanje po stanici.
    ///
    /// Javna je jer je čista funkcija nad snimljenim odgovorima i nosi najviše odluka u
    /// ovom adapteru — testira se direktno, bez mreže.
    /// </summary>
    public static IReadOnlyList<StationReading> Merge(
        IReadOnlyList<WiskiRow> rows,
        Attribution attribution)
    {
        var readings = new List<StationReading>();

        foreach (var group in rows.GroupBy(r => r.StationNo))
        {
            var all = group.ToList();

            // Koordinate nose svi slojevi, ali ne uvijek popunjene. Uzima se prvi red koji
            // ih ima, ne prvi red — inače stanica ostane bez tačke zbog redoslijeda slojeva.
            var identity = all.FirstOrDefault(r => r.Coordinates is not null) ?? all[0];

            var station = new Station
            {
                SourceId = Id,
                StationKey = group.Key,
                Name = identity.StationName,
                River = all.Select(r => r.River).FirstOrDefault(r => r is not null),
                Coordinates = identity.Coordinates,
                ExpectedInterval = ExpectedInterval,
                TypicalPublicationLag = TypicalPublicationLag,
                Attribution = attribution,
            };

            var level = all.FirstOrDefault(r => r.Parameter == ObservationParameter.WaterLevel);

            // Vodostaj se ne ponavlja među ostalim mjerenjima — isti broj na dva mjesta je
            // dva mjesta na kojima se mogu razići.
            var observations = all
                .Where(r => r.Parameter != ObservationParameter.WaterLevel)
                .OrderBy(r => r.Parameter)
                .Select(r => new Observation
                {
                    Parameter = r.Parameter,
                    ParameterLabelOriginal = r.ParameterLabel,
                    Value = r.Value,
                    Unit = r.Unit,
                    MeasuredAt = r.MeasuredAt,
                })
                .ToList();

            if (level is null)
            {
                // Stanica koja mjeri temperaturu a ne vodostaj nije stanica bez podatka.
                // Razlog to mora reći doslovno, inače izgleda kao kvar.
                var what = string.Join(", ", observations.Select(o => o.ParameterLabelOriginal));

                readings.Add(new StationReading.NoData
                {
                    Station = station,
                    StatusLabelOriginal = "",
                    Reason = observations.Count > 0
                        ? $"Ova stanica ne mjeri vodostaj. Mjeri: {what}."
                        : "Izvoz nema nijedno mjerenje za ovu stanicu.",
                    Observations = observations,
                });

                continue;
            }

            readings.Add(new StationReading.Measured
            {
                Station = station,
                // Doslovna klasa iz izvora. Legenda nije objavljena, pa se iz nje **ništa ne
                // izvodi** — ni stupanj, ni boja (zlatno pravilo 3, SOURCES.md §4.5).
                StatusLabelOriginal = level.WaterLevelClass ?? "",
                // Izvoz ne objavljuje nijedan prag.
                ClaimedLevel = AlertLevel.Unknown,
                MeasuredValue = new Measurement(level.Value, level.MeasuredAt),
                Observations = observations,
            });
        }

        return readings;
    }
}
