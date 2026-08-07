/**
 * Šta mapa **ne** pokriva, i zašto.
 *
 * Praznina bez objašnjenja čita se kao „tamo nema ništa za prijaviti" — zlatno pravilo 1 na
 * nivou cijele karte. Sjeveroistok zemlje nije miran, nego neprikazan.
 *
 * Svaka tvrdnja ovdje je provjerena i zapisana u `docs/SOURCES.md` §4.
 */
export function Coverage() {
  return (
    <div className="space-y-2.5 text-xs leading-relaxed text-[--color-text-muted]">
      <p>
        <strong className="font-semibold text-[--color-text]">Republika Srpska.</strong> RHMZ
        Republike Srpske ne objavljuje vodostaje na javno dostupan način. Stranica sa mapom
        automatskih hidroloških stanica im ne radi, a stranica biltena je prazna. Bilteni JU
        „Vode Srpske” su tromjesečni časopis, bez tabela vodostaja.
      </p>

      <p>
        <strong className="font-semibold text-[--color-text]">Brčko distrikt.</strong> Nije nam
        poznat nijedan izvor.
      </p>

      <p className="rounded-[--radius-card] border border-[--color-line] bg-[--color-ink-850] px-3 py-2.5 text-[--color-text-soft]">
        Prazan dio mape ne znači da je tamo mirno — znači da podatak nemamo. Za ta područja
        pratite nadležnu civilnu zaštitu.
      </p>
    </div>
  )
}
