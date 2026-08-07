import type { StationProperties } from '../api/types'

/**
 * Detalj mjernog mjesta.
 *
 * Ovdje **nema vodostaja**, i to nije propust nego činjenica: `HYDRO_ID` na dionicama ne
 * pokazuje na ovaj registar (SOURCES.md §1.7), pa se stanje ne može prikačiti stanici.
 * Prazan prostor bi korisnika ostavio da nagađa zašto, pa to piše doslovno.
 */
export function StationDetail({
  station,
  onClose,
}: {
  station: StationProperties
  onClose: () => void
}) {
  return (
    <article aria-label={`Mjerno mjesto: ${station.name}`} className="p-4">
      <header className="mb-4 flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="truncate text-lg leading-tight font-semibold">{station.name}</h2>
          <p className="mt-0.5 text-sm text-[--color-text-muted]">Mjerno mjesto</p>
        </div>
        <button
          type="button"
          onClick={onClose}
          aria-label="Zatvori detalj"
          className="shrink-0 rounded-[--radius-chip] border border-[--color-line-strong] px-3 py-1 text-xs text-[--color-text-soft] hover:bg-[--color-ink-800] hover:text-[--color-text]"
        >
          Zatvori
        </button>
      </header>

      <dl className="space-y-2 text-sm">
        {station.location && <Row label="Lokacija">{station.location}</Row>}
        {station.stationType && <Row label="Tip">{station.stationType}</Row>}

        <Row label="Kota nule letve">
          {station.gaugeZero === null || station.gaugeZero === undefined ? (
            /* 13 od 102 stanice nemaju kotu nule. Crtica bi izgledala kao nula. */
            <span className="text-[--color-text-muted]">nije objavljena</span>
          ) : (
            <span className="tabular">{station.gaugeZero} m n.v.</span>
          )}
        </Row>

        {station.gaugeBoardCount !== null && station.gaugeBoardCount !== undefined && (
          <Row label="Vodomjernih letvi">
            <span className="tabular">{station.gaugeBoardCount}</span>
          </Row>
        )}
      </dl>

      <p className="mt-4 rounded-[--radius-card] border border-[--color-line] bg-[--color-ink-850] px-3 py-2.5 text-xs leading-relaxed text-[--color-text-soft]">
        Za ovo mjerno mjesto nemamo vodostaj. Agencija objavljuje stanje po dionicama rijeka, a
        veza između dionica i registra stanica nije javno objavljena — pa je ne izmišljamo.
        Stanje pogledaj na dionici na kojoj se mjesto nalazi.
      </p>

      <footer className="mt-4 border-t border-[--color-line] pt-3 text-xs text-[--color-text-muted]">
        Izvor:{' '}
        <a
          href={station.agencyUrl ?? '#'}
          target="_blank"
          rel="noreferrer"
          className="text-[--color-text-soft] underline underline-offset-2 hover:text-[--color-text]"
        >
          {station.agencyName}
        </a>
      </footer>
    </article>
  )
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className="shrink-0 text-[--color-text-muted]">{label}</dt>
      <dd className="text-right">{children}</dd>
    </div>
  )
}
