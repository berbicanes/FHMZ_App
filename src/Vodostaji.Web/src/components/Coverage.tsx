/**
 * Šta mapa **ne** pokriva, i zašto.
 *
 * Praznina bez objašnjenja čita se kao "tamo nema ništa za prijaviti" — zlatno pravilo 1 na
 * nivou cijele karte. Sjeveroistok zemlje nije miran, nego neprikazan, i to mora biti
 * napisano jednako jasno kao i ono što jeste prikazano.
 *
 * Svaka tvrdnja ovdje je provjerena i zapisana u `docs/SOURCES.md` §4.
 */
export function Coverage() {
  return (
    <section aria-label="Šta nije pokriveno" className="text-xs leading-relaxed">
      <h2 className="mb-1 font-semibold tracking-wide text-[--color-text-muted] uppercase">
        Šta nije pokriveno
      </h2>

      <p className="mb-2 text-[--color-text-muted]">
        <strong className="font-semibold text-[--color-text]">Republika Srpska.</strong>{' '}
        RHMZ Republike Srpske ne objavljuje vodostaje na javno dostupan način. Njihova stranica
        sa mapom automatskih hidroloških stanica ne radi, a stranica biltena je prazna. Bilteni
        JU „Vode Srpske” su tromjesečni časopis, bez tabela vodostaja.
      </p>

      <p className="mb-2 text-[--color-text-muted]">
        <strong className="font-semibold text-[--color-text]">Brčko distrikt.</strong>{' '}
        Nije nam poznat nijedan izvor.
      </p>

      <p className="text-[--color-text-muted]">
        Prazan dio mape zato ne znači da je tamo mirno — znači da podatak nemamo. Za ta
        područja pratite nadležnu civilnu zaštitu.
      </p>
    </section>
  )
}
