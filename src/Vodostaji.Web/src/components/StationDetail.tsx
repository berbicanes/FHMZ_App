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
    <aside
      aria-label={`Detalj mjernog mjesta ${station.name}`}
      className="border-t border-[--color-border] bg-[--color-surface-raised] p-4"
    >
      <div className="mb-3 flex items-start justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold">{station.name}</h2>
          <p className="text-xs text-[--color-text-muted]">Mjerno mjesto</p>
        </div>
        <button
          type="button"
          onClick={onClose}
          className="rounded border border-[--color-border] px-2 py-1 text-xs text-[--color-text-muted] hover:text-[--color-text]"
        >
          Zatvori
        </button>
      </div>

      <dl className="space-y-1.5 text-sm">
        {station.location && (
          <div className="flex justify-between gap-4">
            <dt className="shrink-0 text-[--color-text-muted]">Lokacija</dt>
            <dd className="text-right">{station.location}</dd>
          </div>
        )}

        {station.stationType && (
          <div className="flex justify-between gap-4">
            <dt className="text-[--color-text-muted]">Tip</dt>
            <dd className="text-right">{station.stationType}</dd>
          </div>
        )}

        <div className="flex justify-between gap-4">
          <dt className="text-[--color-text-muted]">Kota nule letve</dt>
          <dd className="tabular text-right">
            {station.gaugeZero === null || station.gaugeZero === undefined ? (
              /* 13 od 102 stanice nemaju kotu nule. Crtica bi izgledala kao nula. */
              <span className="text-[--color-text-muted]">nije objavljena</span>
            ) : (
              <>
                {station.gaugeZero} <span className="text-[--color-text-muted]">m n.v.</span>
              </>
            )}
          </dd>
        </div>

        {station.gaugeBoardCount !== null && station.gaugeBoardCount !== undefined && (
          <div className="flex justify-between gap-4">
            <dt className="text-[--color-text-muted]">Vodomjernih letvi</dt>
            <dd className="tabular text-right">{station.gaugeBoardCount}</dd>
          </div>
        )}
      </dl>

      <p className="mt-4 rounded border border-[--color-border] bg-[--color-surface] p-3 text-xs leading-relaxed text-[--color-text-muted]">
        Za ovo mjerno mjesto nemamo vodostaj. Agencija objavljuje stanje po dionicama rijeka, a
        veza između dionica i registra stanica nije javno objavljena — pa je ne izmišljamo.
        Stanje pogledaj na dionici na kojoj se mjesto nalazi.
      </p>

      <p className="mt-3 border-t border-[--color-border] pt-3 text-xs text-[--color-text-muted]">
        Izvor:{' '}
        <a href={station.agencyUrl ?? '#'} target="_blank" rel="noreferrer" className="underline">
          {station.agencyName}
        </a>
      </p>
    </aside>
  )
}
