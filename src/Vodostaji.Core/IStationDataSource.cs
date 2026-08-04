namespace Vodostaji.Core;

/// <summary>
/// Jedan izvor podataka. Adapter zna samo za svoj izvor — nikad za druge adaptere, nikad za
/// bazu, nikad za UI. Pad jednog izvora zato ne može srušiti ostale (zlatno pravilo 5).
/// </summary>
public interface IStationDataSource
{
    /// <summary>Identifikator koji ide u svaki ingest log i u `/api/v1/sources`.</summary>
    string SourceId { get; }

    Attribution Attribution { get; }

    /// <summary>Pretpostavka o vremenskoj zoni, sa dokazom.</summary>
    SourceClock Clock { get; }

    /// <summary>
    /// Najmanji razmak između dva pogotka na izvorne servere. LEGAL.md §2.5 obećava
    /// agencijama najmanje 10 minuta; adapter smije tražiti više, nikad manje.
    /// </summary>
    TimeSpan MinimumFetchInterval { get; }

    Task<SourceFetchResult> FetchAsync(CancellationToken cancellationToken);
}
