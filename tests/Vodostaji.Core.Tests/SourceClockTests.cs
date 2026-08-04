using Vodostaji.Core;

namespace Vodostaji.Core.Tests;

/// <summary>
/// Vrijednosti u ovim testovima nisu izmišljene — dolaze iz snimaka od 2026-08-04
/// zabilježenih u `docs/SOURCES.md`. Kad izvor promijeni konvenciju, ovi testovi padaju,
/// i to je jedini način da to primijetimo prije korisnika.
/// </summary>
public class SourceClockTests
{
    private static readonly SourceClock Avpjm = new()
    {
        Convention = ClockConvention.FixedOffset,
        FixedOffset = TimeSpan.FromHours(1),
        Evidence = "Polje `owner` kaže 'zimsko računanje vremena'; snimak 2026-08-04 daje "
                 + "očitanje 42 min u budućnosti ako se čita kao UTC.",
    };

    private static readonly SourceClock Fhmzbih = new()
    {
        Convention = ClockConvention.LocalWithDst,
        TimeZoneId = "Europe/Sarajevo",
        Evidence = "Martin Brod nosio 5.8.2026 00:00 u trenutku 22:43Z; kao CET bi bio u budućnosti.",
    };

    private static readonly SourceClock AvpSava = new()
    {
        Convention = ClockConvention.Unverified,
        PessimisticOffset = TimeSpan.FromHours(2),
        Evidence = "Zona neriješena, SOURCES.md → Otvorena pitanja. Do dokaza se čita kao CEST, "
                 + "što daje najstariji mogući trenutak.",
    };

    [Fact]
    public void Avpjm_epoch_se_cita_kao_zimsko_vrijeme_a_ne_kao_utc()
    {
        // Zadnje očitanje za Mostar iz snimka 2026-08-04.
        var resolved = Avpjm.ResolveEpochSeconds(1785885300);

        // Naivno čitanje daje 23:15Z, što je bilo 42 minute u budućnosti.
        Assert.Equal(new DateTimeOffset(2026, 8, 4, 22, 15, 0, TimeSpan.Zero), resolved);
    }

    [Fact]
    public void Fhmzbih_zidno_vrijeme_se_cita_sa_ljetnim_pomakom()
    {
        // Martin Brod: "5.8.2026 00:00" lokalno, ljeti, dakle CEST = UTC+2.
        var resolved = Fhmzbih.Resolve(new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal(new DateTimeOffset(2026, 8, 4, 22, 0, 0, TimeSpan.Zero), resolved);
    }

    [Fact]
    public void Fhmzbih_zimi_koristi_zimski_pomak()
    {
        var resolved = Fhmzbih.Resolve(new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal(new DateTimeOffset(2026, 1, 15, 7, 0, 0, TimeSpan.Zero), resolved);
    }

    [Fact]
    public void Avpjm_ne_pomjera_sat_ljeti_jer_ostaje_na_zimskom_vremenu()
    {
        var zima = Avpjm.Resolve(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Unspecified));
        var ljeto = Avpjm.Resolve(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal(new DateTimeOffset(2026, 1, 15, 11, 0, 0, TimeSpan.Zero), zima);
        Assert.Equal(new DateTimeOffset(2026, 7, 15, 11, 0, 0, TimeSpan.Zero), ljeto);

        // Isto zidno vrijeme u obje sezone daje isti pomak — to je cijela razlika u odnosu
        // na FHMZBIH, kod kojeg bi ljetni pomak bio dva sata.
        var pomakZimi = new DateTime(2026, 1, 15, 12, 0, 0) - zima.UtcDateTime;
        var pomakLjeti = new DateTime(2026, 7, 15, 12, 0, 0) - ljeto.UtcDateTime;
        Assert.Equal(pomakZimi, pomakLjeti);
        Assert.Equal(TimeSpan.FromHours(1), pomakLjeti);
    }

    [Fact]
    public void Neverifikovana_zona_daje_najstariji_moguci_trenutak()
    {
        var wall = new DateTime(2026, 8, 4, 21, 0, 0, DateTimeKind.Unspecified);

        var pesimisticno = AvpSava.Resolve(wall);
        var kaoUtc = new SourceClock { Convention = ClockConvention.Utc, Evidence = "test" }.Resolve(wall);

        Assert.Equal(new DateTimeOffset(2026, 8, 4, 19, 0, 0, TimeSpan.Zero), pesimisticno);
        Assert.True(pesimisticno < kaoUtc, "neverifikovana zona mora dati stariji podatak, ne svježiji");
    }

    [Fact]
    public void Proljetni_prelaz_preskace_sat_koji_ne_postoji()
    {
        // Europe/Sarajevo, 29.3.2026: 02:00 -> 03:00. Vrijeme 02:30 tog dana ne postoji.
        var nepostojece = new DateTime(2026, 3, 29, 2, 30, 0, DateTimeKind.Unspecified);

        var ex = Assert.Throws<InvalidTimeZoneTimeException>(() => Fhmzbih.Resolve(nepostojece));
        Assert.Equal("Europe/Sarajevo", ex.TimeZoneId);
    }

    [Fact]
    public void Jesenji_prelaz_bira_raniji_trenutak_pa_podatak_ispada_stariji()
    {
        // Europe/Sarajevo, 25.10.2026: 03:00 -> 02:00. Vrijeme 02:30 postoji dvaput.
        var dvosmisleno = new DateTime(2026, 10, 25, 2, 30, 0, DateTimeKind.Unspecified);

        var resolved = Fhmzbih.Resolve(dvosmisleno);

        // Ljetni pomak (+2) daje 00:30Z, zimski (+1) daje 01:30Z. Biramo raniji.
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), resolved);
    }

    [Fact]
    public void Vrijeme_koje_vec_tvrdi_zonu_se_odbija()
    {
        var vecOdluceno = new DateTime(2026, 8, 4, 21, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => Fhmzbih.Resolve(vecOdluceno));
    }
}
