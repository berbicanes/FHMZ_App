namespace Vodostaji.Probe;

internal sealed record HtmlTarget(string SourceId, string Name, string Url, string Note);

/// <summary>
/// Ciljevi prepisani iz SOURCES.md. Ništa se ovdje ne izvodi iz pretpostavke —
/// ArcGIS servisi se ne nabrajaju ručno nego otkrivaju iz kataloga, a ovdje stoji
/// samo koji su nas od otkrivenih zanimaju dovoljno da se buši do nivoa polja.
/// </summary>
internal static class ProbeTargets
{
    public const string ArcGisRoot = "https://isvportal.voda.ba/server/rest/services";
    public const string ArcGisSourceId = "avp-sava";

    /// <summary>
    /// Servisi iz SOURCES.md §1. Poređenje ide po zadnjem segmentu imena servisa,
    /// case-insensitive, jer ArcGIS katalog vraća imena sa prefiksom foldera.
    /// </summary>
    public static readonly IReadOnlySet<string> ServicesOfInterest =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Hidrolosko_stane_u_realnom_vremenu",
            "Prognoza_hidrološkog_stanja_javno",
            "ISV_BIH_2009_javnakarta",
            "Upravljanje_rizicima_od_poplave___javno",
            "Crowdsource_Flood_public",
        };

    /// <summary>
    /// Slojevi koje SOURCES.md §1 imenuje poimence — samo za njih povlačimo i uzorak podataka,
    /// ne samo shemu. Ključ je kratko ime servisa, vrijednost su id-jevi slojeva i tabela.
    /// </summary>
    private static readonly Dictionary<string, int[]> SampleLayers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Hidrolosko_stane_u_realnom_vremenu"] = [0],
            ["ISV_BIH_2009_javnakarta"] = [1, 50, 98],
        };

    public static bool WantsSample(string serviceShortName, int layerId) =>
        SampleLayers.TryGetValue(serviceShortName, out var ids) && ids.Contains(layerId);

    /// <summary>HTML izvori — po jedna reprezentativna stranica, kako Faza 0 traži.</summary>
    public static readonly IReadOnlyList<HtmlTarget> Html =
    [
        new("avpjm", "vodomjerne-stanice-lista",
            "https://avpjm.jadran.ba/vodomjerne_stanice",
            "SOURCES.md §2 — lista stanica"),

        new("avpjm", "vodomjerne-stanice-detalj-1",
            "https://avpjm.jadran.ba/vodomjerne_stanice/1",
            "SOURCES.md §2 — Hidrološka postaja Mostar, reprezentativan detalj"),

        new("fhmzbih", "hidro-index",
            "https://www.fhmzbih.gov.ba/latinica/HIDRO/",
            "SOURCES.md §3 — dnevni hidrološki pregled"),

        new("fhmzbih", "fop-index",
            "https://fop.fhmzbih.gov.ba",
            "SOURCES.md §3 — pragovi obavještavanja"),
    ];
}
