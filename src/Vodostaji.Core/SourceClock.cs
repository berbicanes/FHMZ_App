namespace Vodostaji.Core;

/// <summary>Kako izvor zapisuje vrijeme. Nijedna od ovih vrijednosti nije podrazumijevana —
/// svaki adapter mora izabrati i obrazložiti (SOURCES.md, kontrolna lista).</summary>
public enum ClockConvention
{
    /// <summary>
    /// Zona nije dokazana. Vrijeme se čita **pesimistično**, tako da podatak ispadne stariji
    /// nego što možda jeste. Grešku u tom smjeru korisnik vidi kao opreznost; greška u drugom
    /// smjeru prikazuje stari podatak kao svjež i krši zlatno pravilo 2.
    /// </summary>
    Unverified = 0,

    /// <summary>Vrijeme je stvarno UTC.</summary>
    Utc,

    /// <summary>
    /// Lokalno vrijeme sa **stalnim** pomakom, bez ljetnog računanja. AVPJM zapisuje zimsko
    /// vrijeme cijele godine i to piše u polju `owner` (SOURCES.md §2).
    /// </summary>
    FixedOffset,

    /// <summary>Lokalno zidno vrijeme sa punim DST pravilima. FHMZBIH (SOURCES.md §3).</summary>
    LocalWithDst,
}

/// <summary>
/// Pretpostavka adaptera o tome šta vrijeme iz izvora znači, zajedno sa dokazom za nju.
///
/// <see cref="Evidence"/> je obavezan i to nije formalnost. Dvije agencije u istoj zemlji
/// koriste dvije različite konvencije, a treća nije riješena — bez zapisanog dokaza niko
/// za šest mjeseci neće znati zašto se negdje oduzima sat, a negdje ne.
/// </summary>
public sealed record SourceClock
{
    public required ClockConvention Convention { get; init; }

    /// <summary>Zašto vjerujemo da je konvencija takva. Ide u dokumentaciju i u log.</summary>
    public required string Evidence { get; init; }

    /// <summary>Pomak za <see cref="ClockConvention.FixedOffset"/>.</summary>
    public TimeSpan FixedOffset { get; init; }

    /// <summary>IANA identifikator za <see cref="ClockConvention.LocalWithDst"/>.</summary>
    public string? TimeZoneId { get; init; }

    /// <summary>
    /// Najveći pomak koji je za <see cref="ClockConvention.Unverified"/> još uvijek moguć.
    /// Koristi se doslovno, da bi podatak ispao najstariji koji bi mogao biti.
    /// </summary>
    public TimeSpan PessimisticOffset { get; init; }

    /// <summary>
    /// Prevodi zidno vrijeme iz izvora u stvarni trenutak.
    /// Ulaz mora biti <see cref="DateTimeKind.Unspecified"/> — vrijeme koje još nije mjesto.
    /// </summary>
    public DateTimeOffset Resolve(DateTime wallClock)
    {
        if (wallClock.Kind != DateTimeKind.Unspecified)
        {
            throw new ArgumentException(
                "Zidno vrijeme mora biti Unspecified. Kind koji već tvrdi zonu znači da je " +
                "negdje ranije donesena odluka koju ovaj tip treba da donese.",
                nameof(wallClock));
        }

        return Convention switch
        {
            ClockConvention.Utc =>
                new DateTimeOffset(wallClock, TimeSpan.Zero),

            ClockConvention.FixedOffset =>
                new DateTimeOffset(wallClock, FixedOffset).ToUniversalTime(),

            ClockConvention.LocalWithDst =>
                ResolveWithTimeZone(wallClock),

            ClockConvention.Unverified =>
                new DateTimeOffset(wallClock, PessimisticOffset).ToUniversalTime(),

            _ => throw new InvalidOperationException($"Nepoznata konvencija: {Convention}"),
        };
    }

    /// <summary>
    /// Prevodi epoch koji izvor predstavlja kao UTC, ali koji to ne mora biti.
    /// Milisekunde se prvo čitaju doslovno, pa se dobijeno zidno vrijeme provuče kroz
    /// <see cref="Resolve"/> — tako se ista pretpostavka primjenjuje na oba oblika ulaza.
    /// </summary>
    public DateTimeOffset ResolveEpochMilliseconds(long milliseconds) =>
        Resolve(DateTime.SpecifyKind(
            DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime,
            DateTimeKind.Unspecified));

    /// <inheritdoc cref="ResolveEpochMilliseconds"/>
    public DateTimeOffset ResolveEpochSeconds(long seconds) =>
        Resolve(DateTime.SpecifyKind(
            DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime,
            DateTimeKind.Unspecified));

    private DateTimeOffset ResolveWithTimeZone(DateTime wallClock)
    {
        if (TimeZoneId is null)
        {
            throw new InvalidOperationException(
                $"{nameof(ClockConvention.LocalWithDst)} traži {nameof(TimeZoneId)}.");
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

        // Sat koji je preskočen na proljetnom prelazu ne postoji. Izvor koji ga ipak pošalje
        // je nalaz — ne izmišljaj vrijeme, reci da ga nema.
        if (zone.IsInvalidTime(wallClock))
        {
            throw new InvalidTimeZoneTimeException(wallClock, TimeZoneId);
        }

        // Sat koji se na jesenjem prelazu ponovi je dvosmislen: isto zidno vrijeme odgovara
        // dvama stvarnim trenucima. Biramo **veći pomak**, jer on daje raniji trenutak, pa
        // podatak ispada stariji. Manji pomak bi ga prikazao svježijim nego što možda jeste.
        if (zone.IsAmbiguousTime(wallClock))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(wallClock);
            return new DateTimeOffset(wallClock, offsets.Max()).ToUniversalTime();
        }

        return new DateTimeOffset(wallClock, zone.GetUtcOffset(wallClock)).ToUniversalTime();
    }
}

/// <summary>Izvor je poslao vrijeme koje u svojoj zoni ne postoji — preskočeni sat na
/// proljetnom DST prelazu. Stanica se preskače i logira, run se ne ruši.</summary>
public sealed class InvalidTimeZoneTimeException(DateTime wallClock, string timeZoneId)
    : Exception($"Vrijeme {wallClock:yyyy-MM-dd HH:mm} ne postoji u zoni {timeZoneId}.")
{
    public DateTime WallClock { get; } = wallClock;

    public string TimeZoneId { get; } = timeZoneId;
}
