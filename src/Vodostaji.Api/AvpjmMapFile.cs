using System.Text;

namespace Vodostaji.Api;

/// <summary>
/// GeoJSON sloja AVPJM-a. Zaseban fajl jer je zaseban sloj — dionice AVP Save i stanice
/// AVPJM-a se ne stapaju ni u jedan odgovor.
/// </summary>
public sealed class AvpjmMapFile(string path, ILogger<AvpjmMapFile> logger)
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
            logger.LogInformation("AVPJM sloj prepisan: {Bytes} bajta.", geoJson.Length);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> ReadAsync(CancellationToken cancellationToken) =>
        Exists ? await File.ReadAllTextAsync(Path, cancellationToken).ConfigureAwait(false) : null;
}
