namespace Vodostaji.Core;

/// <summary>
/// Trajno stanje po izvoru.
///
/// Namjerno **nema metode za brisanje**. Zlatno pravilo 5 kaže da kad fetch ne uspije, stari
/// podatak ostaje sa poštenim timestampom i nikad se ne briše — najlakši način da se to
/// obezbijedi je da brisanje ne postoji u ugovoru.
/// </summary>
public interface IReadingStore
{
    /// <summary>
    /// Upisuje uspješno povlačenje. Zamjenjuje vrijednosti za stanice koje su u njemu,
    /// a stanice kojih u njemu nema ostaju netaknute — nestanak stanice iz jednog odgovora
    /// nije dokaz da je stanica prestala postojati.
    /// </summary>
    Task SaveAsync(SourceFetchResult result, CancellationToken cancellationToken);

    /// <summary>Koliko stanica trenutno imamo za taj izvor. Koristi se da se prepozna
    /// odgovor koji je formalno uspio a stigao prazan.</summary>
    Task<int> CountAsync(string sourceId, CancellationToken cancellationToken);
}
