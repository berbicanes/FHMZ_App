using Vodostaji.Core;

namespace Vodostaji.Data;

/// <summary>
/// Registar stanica. Upisuje se i dopunjava, nikad ne prazni — stanica koja izostane iz
/// jednog odgovora nije dokaz da je prestala postojati.
/// </summary>
public class StationRow
{
    public required string SourceId { get; set; }

    public required string StationKey { get; set; }

    public required string Name { get; set; }

    public string? River { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public decimal? GaugeZero { get; set; }

    /// <summary>Očekivani razmak između mjerenja, u sekundama. Po stanici, ne po izvoru.</summary>
    public required long ExpectedIntervalSeconds { get; set; }

    public required string AgencyName { get; set; }

    public required string AgencyUrl { get; set; }

    public string? SourceUrl { get; set; }

    public required DateTimeOffset FirstSeenAt { get; set; }

    public required DateTimeOffset LastSeenAt { get; set; }
}

/// <summary>
/// Trenutno stanje stanice — ono što mapa crta.
///
/// <see cref="MeasuredAt"/> i <see cref="FetchedAt"/> stoje kao dvije kolone i nikad se ne
/// miješaju. Prikaz `FetchedAt`-a kao vremena mjerenja je najlakši način da se prekrši
/// zlatno pravilo 2, pa u shemi ni ne postoji polje koje bi ih spojilo.
/// </summary>
public class StationStateRow
{
    public required string SourceId { get; set; }

    public required string StationKey { get; set; }

    /// <summary>
    /// Null znači da podatka nema. Nema zasebne "je li poznato" zastavice koja bi mogla
    /// otići van korака sa vrijednošću — odsustvo vrijednosti **jeste** odsustvo podatka.
    /// </summary>
    public decimal? ValueCm { get; set; }

    /// <summary>Kad je izmjereno, u UTC-u. Null kad vrijednosti nema.</summary>
    public DateTimeOffset? MeasuredAt { get; set; }

    /// <summary>Kad smo povukli. Uvijek postoji, i nikad se ne prikazuje kao vrijeme mjerenja.</summary>
    public required DateTimeOffset FetchedAt { get; set; }

    /// <summary>
    /// `Unknown` je nula u <see cref="AlertLevel"/>, pa i kolona sa podrazumijevanom
    /// vrijednošću ispada nepoznata umjesto normalna.
    /// </summary>
    public required AlertLevel Level { get; set; }

    public required string StatusLabelOriginal { get; set; }

    /// <summary>Zašto podatka nema, kad ga nema.</summary>
    public string? NoDataReason { get; set; }

    /// <summary>Pragovi kao JSON, jer ih svaki izvor imenuje i broji drugačije.</summary>
    public string? ThresholdsJson { get; set; }

    public string? ThresholdsDefinedBy { get; set; }
}

/// <summary>
/// Historija mjerenja, samo dodavanje.
///
/// Jedinstveni indeks na (izvor, stanica, vrijeme mjerenja) čini ingest idempotentnim:
/// AVP Sava se osvježava na sat a mi pitamo svakih 15 minuta, pa bi bez njega isti podatak
/// ušao četiri puta i graf u Fazi 2 bi imao stepenice kojih u rijeci nema.
/// </summary>
public class MeasurementRow
{
    public long Id { get; set; }

    public required string SourceId { get; set; }

    public required string StationKey { get; set; }

    public required decimal ValueCm { get; set; }

    public required DateTimeOffset MeasuredAt { get; set; }

    /// <summary>Stupanj koji je izvor tvrdio u trenutku tog mjerenja.</summary>
    public required AlertLevel Level { get; set; }

    public required DateTimeOffset FirstFetchedAt { get; set; }
}
