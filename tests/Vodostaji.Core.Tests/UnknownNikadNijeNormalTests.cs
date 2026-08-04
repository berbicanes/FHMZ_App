using Vodostaji.Core;

namespace Vodostaji.Core.Tests;

/// <summary>
/// Zlatno pravilo 1. Ovi testovi ne provjeravaju jednu funkciju nego jedan **oblik modela** —
/// da nepoznato stanje nema kuda da postane normalno.
/// </summary>
public class UnknownNikadNijeNormalTests
{
    private static readonly Attribution BiloKoja = new()
    {
        AgencyName = "test",
        AgencyUrl = new Uri("https://example.invalid"),
    };

    private static readonly Station BiloKoja_Stanica = new()
    {
        SourceId = "test",
        StationKey = "1",
        Name = "Test",
        ExpectedInterval = TimeSpan.FromHours(1),
        Attribution = BiloKoja,
    };

    [Fact]
    public void Zaboravljen_stupanj_ispada_unknown_a_ne_normal()
    {
        Assert.Equal(AlertLevel.Unknown, default(AlertLevel));
        Assert.NotEqual(AlertLevel.Normal, default(AlertLevel));
    }

    [Fact]
    public void NoData_je_uvijek_unknown_bez_obzira_na_sve_ostalo()
    {
        var reading = new StationReading.NoData
        {
            Station = BiloKoja_Stanica,
            StatusLabelOriginal = "Nema podataka o vodostaju",
            Reason = "DATE_TIME je null",
            Thresholds = new Thresholds
            {
                DefinedBy = "AVP Sava",
                Values = [new Threshold("REGULAR_DEF_ST", 300m, AlertLevel.Elevated)],
            },
        };

        Assert.Equal(AlertLevel.Unknown, reading.Level);
        Assert.Null(reading.Measurement);

        // Ni kopija sa izmjenama ne može promijeniti stupanj — nema svojstva za njega.
        var kopija = reading with { Reason = "nesto drugo" };
        Assert.Equal(AlertLevel.Unknown, kopija.Level);
    }

    [Fact]
    public void NoData_nema_nijedno_svojstvo_u_koje_bi_se_stupanj_upisao()
    {
        // Ovo je tvrdnja o obliku tipa, ne o vrijednosti. Ako neko sutra doda settabilan
        // `Level` na NoData, test pada i objašnjava zašto se to ne smije.
        var settable = typeof(StationReading.NoData)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(AlertLevel) && p.CanWrite)
            .ToList();

        Assert.Empty(settable);
    }

    [Fact]
    public void Izmjeren_podatak_sa_neprepoznatim_statusom_ostaje_unknown()
    {
        // Imamo broj, ali izvor je poslao status koji nije u našem rječniku.
        // Broj bez tvrdnje nije normala.
        var reading = new StationReading.Measured
        {
            Station = BiloKoja_Stanica,
            StatusLabelOriginal = "Нешто сасвим ново",
            ClaimedLevel = AlertLevel.Unknown,
            MeasuredValue = new Measurement(17.6m, new DateTimeOffset(2026, 8, 4, 21, 0, 0, TimeSpan.Zero)),
        };

        Assert.Equal(AlertLevel.Unknown, reading.Level);
        Assert.NotNull(reading.Measurement);
    }

    [Fact]
    public void Unknown_ne_prolazi_poredjenje_po_pragu_ni_u_jednom_smjeru()
    {
        Assert.False(AlertLevel.Unknown.IsAtLeast(AlertLevel.Normal));
        Assert.False(AlertLevel.Unknown.IsAtLeast(AlertLevel.Emergency));
        Assert.False(AlertLevel.Emergency.IsAtLeast(AlertLevel.Unknown));
        Assert.False(AlertLevel.Unknown.IsKnown());

        Assert.True(AlertLevel.Flood.IsAtLeast(AlertLevel.Elevated));
        Assert.True(AlertLevel.Normal.IsKnown());
    }

    [Fact]
    public void Original_statusa_se_cuva_i_kad_je_mapiranje_uspjelo()
    {
        var reading = new StationReading.Measured
        {
            Station = BiloKoja_Stanica,
            StatusLabelOriginal = "Standby",
            ClaimedLevel = AlertLevel.Normal,
            MeasuredValue = new Measurement(17.6m, DateTimeOffset.UnixEpoch),
        };

        Assert.Equal("Standby", reading.StatusLabelOriginal);
    }
}
