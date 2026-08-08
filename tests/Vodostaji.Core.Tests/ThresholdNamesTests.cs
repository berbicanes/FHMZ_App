using Vodostaji.Core;

namespace Vodostaji.Core.Tests;

/// <summary>
/// Jedini prevod u projektu, pa ima i najstroži test.
///
/// Zlatno pravilo 3 zabranjuje izmišljanje semantike pragova. Ovo prolazi jer aliasi AVP
/// Save sami jesu prevod sa bosanskog — četiri stepena odbrane od poplava — pa je povratak
/// rekonstrukcija originala. Test čuva granicu: prevodi se **samo** ono što je u tabeli, a
/// sve ostalo prolazi netaknuto.
/// </summary>
public class ThresholdNamesTests
{
    [Theory]
    [InlineData("Standby status", "Stanje pripravnosti")]
    [InlineData("Regular defence status", "Redovna odbrana od poplava")]
    [InlineData("Outstanding defence status", "Vanredna odbrana od poplava")]
    [InlineData("Emergency status", "Stanje ugroženosti")]
    public void Cetiri_stepena_odbrane_dobijaju_bosanski_naziv(string original, string expected)
    {
        Assert.Equal(expected, ThresholdNames.Display(original));
        Assert.True(ThresholdNames.IsTranslated(original));
    }

    [Fact]
    public void Natpis_koji_nije_u_tabeli_prolazi_netaknut()
    {
        // FHMZBIH svoj prag već imenuje na bosanskom. Diranje bi bilo prepravljanje izvora.
        const string fhmzbih = "Kontinuirano obavještavanje stanovništva i CZ";

        Assert.Equal(fhmzbih, ThresholdNames.Display(fhmzbih));
        Assert.False(ThresholdNames.IsTranslated(fhmzbih));
    }

    [Fact]
    public void Nepoznat_natpis_se_ne_pogadja()
    {
        // Kad agencija sutra doda peti prag, on se prikazuje onako kako ga ona zove —
        // ne izvodi se iz imena, i ne izostavlja se.
        Assert.Equal("Nesto Novo ST", ThresholdNames.Display("Nesto Novo ST"));
        Assert.False(ThresholdNames.IsTranslated("Nesto Novo ST"));
    }

    [Fact]
    public void Prazan_natpis_ostaje_prazan_a_ne_puca()
    {
        Assert.Equal("", ThresholdNames.Display(""));
    }
}
