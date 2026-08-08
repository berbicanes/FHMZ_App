namespace Vodostaji.Core;

/// <summary>
/// Bosanski naziv za prag, kad postoji ustaljen termin za ono što izvor imenuje.
///
/// <para>
/// Ovo je jedini prevod u projektu i traži obrazloženje, jer zlatno pravilo 3 zabranjuje
/// izmišljanje semantike pragova.
/// </para>
///
/// <para>
/// Obrazloženje: aliasi AVP Save (<c>Standby status</c>, <c>Regular defence status</c>,
/// <c>Outstanding defence status</c>, <c>Emergency status</c>) **sami su prevod** sa
/// bosanskog. To su četiri stepena odbrane od poplava iz federalnog operativnog plana —
/// pripravnost, redovna odbrana, vanredna odbrana, stanje ugroženosti. Doslovnost prevoda
/// se vidi u <c>Outstanding defence</c>, što je kalk od <em>vanredna odbrana</em> i na
/// engleskom ne znači ništa; niko ko piše engleski od nule ne bi tako nazvao taj stepen.
/// Vraćanje na bosanski je dakle **rekonstrukcija originala**, ne nova tvrdnja.
/// </para>
///
/// <para>
/// Zbog toga se original nikad ne odbacuje: <see cref="ReachThreshold.LabelOriginal"/> ide
/// uz svaki prag i UI ga prikazuje. Ako je ova rekonstrukcija pogrešna, korisnik to vidi.
/// </para>
///
/// <para>
/// Ostali izvori se ne diraju. FHMZBIH svoj prag već imenuje na bosanskom
/// („Kontinuirano obavještavanje stanovništva i CZ"), pa ovdje nema šta tražiti.
/// </para>
/// </summary>
public static class ThresholdNames
{
    private static readonly Dictionary<string, string> Bosnian = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Standby status"] = "Stanje pripravnosti",
        ["Regular defence status"] = "Redovna odbrana od poplava",
        ["Outstanding defence status"] = "Vanredna odbrana od poplava",
        ["Emergency status"] = "Stanje ugroženosti",
    };

    /// <summary>
    /// Bosanski naziv, ili sam original kad ustaljenog termina nema.
    ///
    /// Nikad ne vraća prazno i nikad ne pogađa: natpis koji nije u tabeli prolazi netaknut.
    /// </summary>
    public static string Display(string labelOriginal) =>
        Bosnian.TryGetValue(labelOriginal.Trim(), out var name) ? name : labelOriginal;

    /// <summary>Da li je natpis preveden — UI iz ovoga zna treba li pokazati i original.</summary>
    public static bool IsTranslated(string labelOriginal) =>
        Bosnian.ContainsKey(labelOriginal.Trim());
}
