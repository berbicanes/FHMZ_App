using System.Text;

namespace Vodostaji.Api;

/// <summary>
/// GeoJSON koji mapa čita, kao fajl na disku koji ingest job prepisuje.
///
/// Statički fajl umjesto upita po zahtjevu: mapa se otvara mnogo češće nego što se podaci
/// mijenjaju, a aplikacija mora biti brza na slaboj vezi (CLAUDE.md → Šta NE raditi).
///
/// Upis ide preko privremenog fajla pa se preimenuje. Preimenovanje je atomsko, pa čitalac
/// nikad ne dobije polovinu fajla — a polovina GeoJSON-a bi u browseru bila prazna mapa,
/// što izgleda kao "nema opasnosti" umjesto "nema podatka".
/// </summary>
public sealed class ReachMapFile(string path, ILogger<ReachMapFile> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string Path { get; } = path;

    public bool Exists => File.Exists(Path);

    public async Task WriteAsync(string geoJson, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = Path + ".tmp";
            await File.WriteAllTextAsync(temporary, geoJson, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);

            File.Move(temporary, Path, overwrite: true);

            logger.LogInformation("Mapa prepisana: {Bytes} bajta u {Path}.", geoJson.Length, Path);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!Exists)
        {
            return null;
        }

        return await File.ReadAllTextAsync(Path, cancellationToken).ConfigureAwait(false);
    }
}
