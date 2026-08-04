namespace Vodostaji.Core;

/// <summary>
/// Jedino mjesto u kodu gdje stoji kontakt koji odlazi na servere agencija.
/// LEGAL.md §2.6 traži User-Agent koji identifikuje aplikaciju i sadrži kontakt.
/// Kad projekat dobije vlastitu domenu, mijenja se samo ovdje.
///
/// Živi u Core-u, a ne u pojedinom projektu, jer ga koriste i sonda i aplikacija —
/// dvije kopije bi značile da jedna od njih jednog dana ostane na staroj adresi.
/// </summary>
public static class Contact
{
    public const string AppName = "VodostajiBiH";
    public const string Version = "0.1";
    public const string Email = "berbicanes6+vodostaji@gmail.com";

    public static string UserAgent => $"{AppName}/{Version} (+mailto:{Email})";

    /// <summary>User-Agent za sondu, da se u njihovim logovima razlikuje od aplikacije.</summary>
    public static string ProbeUserAgent => $"{AppName}-Probe/{Version} (+mailto:{Email})";
}
