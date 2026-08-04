namespace Vodostaji.Probe;

internal sealed record PageTarget(string SourceId, string Name, string Url, string Extension, string Note);

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

    /// <summary>
    /// Izvori koji nisu ArcGIS — po jedna reprezentativna stranica ili odgovor, kako Faza 0 traži.
    /// Nijedna adresa ovdje nije pogođena naslijepo: svaka je pročitana sa stranice ili iz
    /// skripte koju ta stranica učitava.
    /// </summary>
    public static readonly IReadOnlyList<PageTarget> Pages =
    [
        new("avpjm", "vodomjerne-stanice-lista",
            "https://avpjm.jadran.ba/vodomjerne_stanice", "html",
            "SOURCES.md §2 — lista stanica"),

        new("avpjm", "vodomjerne-stanice-detalj-1",
            "https://avpjm.jadran.ba/vodomjerne_stanice/1", "html",
            "SOURCES.md §2 — Hidrološka postaja Mostar, reprezentativan detalj"),

        new("fhmzbih", "hidro-index",
            "https://www.fhmzbih.gov.ba/latinica/HIDRO/", "html",
            "SOURCES.md §3 — dnevni hidrološki pregled"),

        new("fhmzbih", "hvs-zenica",
            "https://www.fhmzbih.gov.ba/latinica/HIDRO/hvsZenica.php", "html",
            "SOURCES.md §3 — stanica sa satnim osvježavanjem, nosi koordinate"),

        new("fhmzbih", "fop-index",
            "https://fop.fhmzbih.gov.ba", "html",
            "SOURCES.md §3 — pragovi obavještavanja"),

        new("rhmz-rs", "api-flood-defense-points",
            "https://rhmzrs.com/api/flood-defense-points", "json",
            "SOURCES.md §4 — kote odbrane od poplava, pragovi po tački"),

        new("rhmz-rs", "api-meteo-stations",
            "https://rhmzrs.com/api/meteo-stations", "json",
            "SOURCES.md §4 — meteorološke stanice; hidroloških nema"),

        new("rhmz-rs", "hidrologija-mapa-stanica",
            "https://rhmzrs.com/page/hidrologija-mapa-stanica", "html",
            "SOURCES.md §4 — mapa automatskih hidroloških stanica"),

        new("rhmz-rs", "hydro-stations-leaflet-js",
            "https://rhmzrs.com/js/hydro-stations-leaflet.js", "js",
            "SOURCES.md §4 — skripta te mape; dokaz da traži nedefinisan `config`"),

        new("rhmz-rs", "bilten-izvjestaj-o-vodostanju",
            "https://rhmzrs.com/page/bilten-izvjestaj-o-vodostanju", "html",
            "SOURCES.md §4 — bilteni o vodostanju"),

        new("rhmz-rs", "kote-odbrane-od-poplava",
            "https://rhmzrs.com/page/kote-odbrane-od-poplava", "html",
            "SOURCES.md §4 — stranica koja otkriva /api/flood-defense-points"),

        new("vode-srpske", "index",
            "http://www.voders.org/", "html",
            "SOURCES.md §4 — JU Vode Srpske, samo HTTP"),

        new("vode-srpske", "bilteni",
            "http://www.voders.org/javnost/bilten/", "html",
            "SOURCES.md §4 — arhiva biltena u PDF-u"),
    ];
}
