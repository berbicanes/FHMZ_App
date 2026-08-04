using Vodostaji.Core;

namespace Vodostaji.Ingest.AvpSava;

/// <summary>
/// Prevod `CURRENT_STATUS` u <see cref="AlertLevel"/>.
///
/// Vrijednosti nisu izabrane nego prepisane iz renderera sloja
/// `Hidrolosko_stane_u_realnom_vremenu/FeatureServer/0`, snimljenog 2026-08-04
/// (SOURCES.md §1.1). Renderer je ono što agencija stvarno crta na svojoj mapi, pa je
/// to i najbliže tome da agencija sama kaže šta koji status znači.
///
/// Sve što nije u ovoj tabeli je <see cref="AlertLevel.Unknown"/>. Nema grane koja bi
/// nepoznat status svela na `Normal`.
/// </summary>
public static class AvpSavaStatusMap
{
    private static readonly Dictionary<string, AlertLevel> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Standby"] = AlertLevel.Normal,
        ["Regular defence"] = AlertLevel.Elevated,
        ["Outstanding defence"] = AlertLevel.Flood,
        ["Emergency"] = AlertLevel.Emergency,

        // Izvor eksplicitno kaže da podatka nema. To nije stupanj nego njegovo odsustvo,
        // pa se ovdje mapira u Unknown, a parser od takvog zapisa pravi NoData.
        ["No Data"] = AlertLevel.Unknown,
    };

    /// <summary>Da li izvor ovim statusom tvrdi da podatka nema.</summary>
    public static bool MeansNoData(string? status) =>
        string.Equals(status?.Trim(), "No Data", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Prevodi status. Nepoznat, prazan ili null status daje <see cref="AlertLevel.Unknown"/> —
    /// nikad `Normal`, ma koliko to izgledalo kao razuman podrazumijevani izbor.
    /// </summary>
    public static AlertLevel ToAlertLevel(string? status) =>
        status is not null && Known.TryGetValue(status.Trim(), out var level)
            ? level
            : AlertLevel.Unknown;

    /// <summary>Da li je status uopšte u rječniku koji smo vidjeli 2026-08-04. Koristi se da
    /// se promjena rječnika izvora primijeti i logira, umjesto da tiho postane Unknown.</summary>
    public static bool IsRecognised(string? status) =>
        status is not null && Known.ContainsKey(status.Trim());
}
