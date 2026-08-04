namespace Vodostaji.Probe;

/// <summary>
/// Jedino mjesto u kodu gdje stoji kontakt koji odlazi na servere agencija.
/// LEGAL.md §2.6 traži User-Agent koji identifikuje aplikaciju i sadrži kontakt.
/// Kad projekat dobije vlastitu domenu, mijenja se samo ovdje.
/// </summary>
internal static class Contact
{
    public const string AppName = "VodostajiBiH-Probe";
    public const string Version = "0.1";
    public const string Email = "berbicanes6+vodostaji@gmail.com";

    public static string UserAgent => $"{AppName}/{Version} (+mailto:{Email})";
}
