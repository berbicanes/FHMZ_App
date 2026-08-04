using Vodostaji.Core;

namespace Vodostaji.Ingest.AvpSava;

/// <summary>
/// Legenda AVP Save — boje i natpisi doslovno iz renderera sloja, snimljenog 2026-08-04
/// (SOURCES.md §1.1).
///
/// Ovo je legenda **jedne agencije**, ne globalna. CLAUDE.md izričito zabranjuje stapanje
/// slojeva različitih agencija u jedan sloj sa jednom legendom, pa svaki sljedeći izvor
/// donosi svoju — a ne posuđuje ovu.
/// </summary>
public static class AvpSavaLegend
{
    /// <summary>Boja iz renderera. Siva za nepoznato nije izbor stila nego dio njihove legende
    /// (`No Data` → `#CCCCCC`), i mora se vizuelno razlikovati od zelene za normalno.</summary>
    public static string Color(AlertLevel level) => level switch
    {
        AlertLevel.Normal => "#38A800",
        AlertLevel.Elevated => "#FFFF00",
        AlertLevel.Flood => "#FFAA00",
        AlertLevel.Emergency => "#E60000",
        AlertLevel.Unknown => "#CCCCCC",
        _ => "#CCCCCC",
    };

    /// <summary>Natpis kako ga agencija piše na svojoj mapi.</summary>
    public static string Label(AlertLevel level) => level switch
    {
        AlertLevel.Normal => "Normalno",
        AlertLevel.Elevated => "Izljevanje iz korita",
        AlertLevel.Flood => "Poplave",
        AlertLevel.Emergency => "Značajne poplave",
        AlertLevel.Unknown => "Nema podataka",
        _ => "Nema podataka",
    };
}
