namespace Vodostaji.Core;

/// <summary>
/// Stanje jedne stanice u jednom trenutku.
///
/// Zatvorena hijerarhija sa tačno dva oblika, i to je namjerno. Zlatno pravilo 1 kaže da
/// `Unknown` nikad ne postaje `Normal`. Umjesto da se to čuva provjerama koje neko može
/// zaboraviti, čuva se oblikom tipa: <see cref="NoData"/> **nema svojstvo u koje bi se
/// stupanj opasnosti upisao**. Ne postoji dodjela koja bi ga pretvorila u `Normal`, jer ne
/// postoji mjesto za tu vrijednost.
///
/// Konstruktor je privatan, a nasljeđivanje moguće samo ugniježđenim tipovima ispod, pa
/// treći oblik ne može nastati izvan ovog fajla.
/// </summary>
public abstract record StationReading
{
    private StationReading()
    {
    }

    public required Station Station { get; init; }

    /// <summary>
    /// Tekst statusa doslovno kako ga je izvor napisao (`Standby`, `Nema podataka o vodostaju`).
    /// Čuva se uvijek, i kad je mapiranje uspjelo — bez njega ne možemo pokazati korisniku šta
    /// je agencija stvarno rekla, ni primijetiti da se rječnik izvora promijenio.
    /// </summary>
    public required string StatusLabelOriginal { get; init; }

    /// <summary>Pragovi agencije, kad ih izvor daje. Prikazuju se, iz njih se ne zaključuje.</summary>
    public Thresholds? Thresholds { get; init; }

    /// <summary>Stupanj opasnosti kako ga tvrdi izvor.</summary>
    public abstract AlertLevel Level { get; }

    /// <summary>Izmjerena vrijednost, ako je ima.</summary>
    public abstract Measurement? Measurement { get; }

    /// <summary>
    /// Stanica ima podatak. Stupanj i dalje može biti `Unknown` — ako izvor pošalje status koji
    /// ne prepoznajemo, imamo broj ali ne znamo šta o njemu tvrde. Broj bez tvrdnje nije normala.
    /// </summary>
    public sealed record Measured : StationReading
    {
        public required Measurement MeasuredValue { get; init; }

        /// <summary>
        /// Stupanj koji izvor **tvrdi**. Ime je namjerno — ovo nikad nije naš zaključak izveden
        /// iz vrijednosti i pragova, što zabranjuje zlatno pravilo 3.
        /// </summary>
        public required AlertLevel ClaimedLevel { get; init; }

        /// <summary>
        /// Trend koji **izvor objavljuje**, kad ga objavljuje.
        ///
        /// FHMZBIH ga daje kao oznaku (`R` raste, `O` opada, `S` stagnira); AVP Sava i AVPJM
        /// ga ne daju. Kad postoji, ima prednost nad našim izvodom iz dva očitanja — tvrdnja
        /// agencije je jača od našeg računa (zlatno pravilo 3).
        /// </summary>
        public PublishedTrend? Trend { get; init; }

        public override AlertLevel Level => ClaimedLevel;

        public override Measurement? Measurement => MeasuredValue;
    }

    /// <summary>
    /// Stanica nema podatak. Nema svojstvo za stupanj jer nema šta da se u njega upiše.
    /// </summary>
    public sealed record NoData : StationReading
    {
        /// <summary>Zašto podatka nema — izvor je rekao `No Data`, parsiranje je palo, vrijeme
        /// je nedostajalo. Ide u log i u UI, jer "zašto" je korisniku informacija.</summary>
        public required string Reason { get; init; }

        /// <summary>Jedino mjesto u sistemu gdje odsustvo podatka postaje stupanj opasnosti.
        /// Konstanta, ne izraz — nema grane koja bi ikad vratila nešto drugo.</summary>
        public override AlertLevel Level => AlertLevel.Unknown;

        public override Measurement? Measurement => null;
    }
}
