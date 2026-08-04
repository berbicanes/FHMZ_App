namespace Vodostaji.Core;

/// <summary>
/// Stupanj opasnosti. Redoslijed članova je namjeran i nosi dvije garancije.
///
/// `Unknown` je nula, pa je `default(AlertLevel)` uvijek `Unknown`. Nijedno polje koje
/// neko zaboravi postaviti ne može ispasti `Normal` — zaboravljeno stanje je nepoznato
/// stanje, što je i istina.
///
/// `Unknown` nije "ispod" `Normal` nego pored njega. Ne poredi ove vrijednosti relacijskim
/// operatorima; `level >= AlertLevel.Elevated` bi `Unknown` svrstao ispod praga, a mi o njemu
/// ne znamo ništa. Koristi <see cref="AlertLevelExtensions.IsAtLeast"/>.
/// </summary>
public enum AlertLevel
{
    /// <summary>Nemamo podatak. Nije isto što i "nema opasnosti".</summary>
    Unknown = 0,

    /// <summary>Izvor tvrdi da je stanje normalno. Samo tvrdnja izvora, nikad naš zaključak.</summary>
    Normal = 1,

    /// <summary>Izljevanje iz korita.</summary>
    Elevated = 2,

    /// <summary>Poplave.</summary>
    Flood = 3,

    /// <summary>Značajne poplave.</summary>
    Emergency = 4,
}

public static class AlertLevelExtensions
{
    /// <summary>
    /// Da li je stanje dokazano na tom stupnju ili iznad. `Unknown` je uvijek `false` —
    /// ne zato što je nisko, nego zato što o njemu nemamo osnov za tvrdnju.
    /// </summary>
    public static bool IsAtLeast(this AlertLevel level, AlertLevel threshold) =>
        level != AlertLevel.Unknown && threshold != AlertLevel.Unknown && level >= threshold;

    /// <summary>Da li podatak uopšte postoji. Postoji tačno jedno mjesto gdje se ovo pita.</summary>
    public static bool IsKnown(this AlertLevel level) => level != AlertLevel.Unknown;
}
