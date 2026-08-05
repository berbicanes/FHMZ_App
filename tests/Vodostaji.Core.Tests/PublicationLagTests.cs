using Vodostaji.Core;

namespace Vodostaji.Core.Tests;

/// <summary>
/// Starost se mjeri od trenutka kad je podatak realno mogao stići, ne od mjerenja.
///
/// AVP Sava mjeri na sat a objavljuje 85–115 minuta kasnije, pa bi bez ovoga svaka dionica
/// trajno stajala kao "kasni". Signal koji je uvijek upaljen korisnik prestane gledati, a to
/// je gore nego da ga nema.
/// </summary>
public class PublicationLagTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 0, 0, 0, TimeSpan.Zero);

    private static Station Station(TimeSpan interval, TimeSpan lag) => new()
    {
        SourceId = "avp-sava",
        StationKey = "1",
        Name = "Bosna-Zenica",
        ExpectedInterval = interval,
        TypicalPublicationLag = lag,
        Attribution = new Attribution
        {
            AgencyName = "test",
            AgencyUrl = new Uri("https://example.invalid"),
        },
    };

    [Fact]
    public void Podatak_unutar_kasnjenja_objave_je_najsvjeziji_moguci()
    {
        var station = Station(TimeSpan.FromHours(1), TimeSpan.FromMinutes(115));

        // Izmjereno prije 118 minuta — tik iza uobičajenog kašnjenja objave.
        var missed = station.MissedCycles(Now.AddMinutes(-118), Now);

        Assert.NotNull(missed);
        Assert.True(missed < 1, $"zdravo očitanje ne smije stajati kao propušten ciklus, bilo je {missed}");
    }

    [Fact]
    public void Propusten_ciklus_se_broji_kao_jedan()
    {
        var station = Station(TimeSpan.FromHours(1), TimeSpan.FromMinutes(115));

        // Sat vremena preko kašnjenja znači da je jedno mjerenje izostalo.
        var missed = station.MissedCycles(Now.AddMinutes(-175), Now);

        Assert.NotNull(missed);
        Assert.Equal(1.0, missed!.Value, 1);
    }

    [Fact]
    public void Tri_propustena_ciklusa_prelaze_prag_zastarjelosti()
    {
        var station = Station(TimeSpan.FromHours(1), TimeSpan.FromMinutes(115));

        var missed = station.MissedCycles(Now.AddMinutes(-115 - 190), Now);

        // UI.md §2: preko 3× interval je "podatak zastario".
        Assert.True(missed > 3, $"očekivano preko 3 propuštena ciklusa, bilo je {missed}");
    }

    [Fact]
    public void Bez_kasnjenja_objave_racun_ostaje_obicna_starost()
    {
        var station = Station(TimeSpan.FromHours(1), TimeSpan.Zero);

        Assert.Equal(2.0, station.MissedCycles(Now.AddHours(-2), Now)!.Value, 1);
    }

    [Fact]
    public void Podatak_iz_buducnosti_ne_daje_negativan_broj_ciklusa()
    {
        var station = Station(TimeSpan.FromHours(1), TimeSpan.FromMinutes(115));

        // Negativan omjer bi u UI-u prošao kao "svježije od najsvježijeg", što nije stanje
        // koje postoji. Validacija takav zapis ionako pretvara u NoData.
        Assert.Equal(0, station.MissedCycles(Now.AddMinutes(30), Now));
    }

    [Fact]
    public void Bez_mjerenja_nema_ni_starosti()
    {
        var station = Station(TimeSpan.FromHours(1), TimeSpan.FromMinutes(115));

        // Odsustvo podatka nije stepen starosti nego vlastito stanje.
        Assert.Null(station.MissedCycles(null, Now));
    }
}
