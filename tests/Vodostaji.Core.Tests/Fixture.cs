namespace Vodostaji.Core.Tests;

/// <summary>
/// Učitava snimljene odgovore izvora iz `tests/fixtures/`.
///
/// Testovi adaptera rade isključivo protiv ovih fajlova — bez mreže, bez baze, bez kontejnera.
/// Svaki fixture nosi datum u imenu, pa test koji padne kaže i kad je izvor izgledao drugačije.
/// </summary>
internal static class Fixture
{
    private static readonly Lazy<string> Root = new(FindRoot);

    public static string Read(string relativePath)
    {
        var path = Path.Combine(Root.Value, relativePath);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Fixture ne postoji: {relativePath}. Pokreni `dotnet run --project tools/Probe`.",
                path);
        }

        return File.ReadAllText(path);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "fixtures");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Nije nađen `tests/fixtures` ni u jednom roditelju.");
    }
}
