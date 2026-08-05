import { useId, useMemo, useState } from 'react'
import type { ReachProperties, StationProperties } from '../api/types'
import { searchReaches, searchStations, type SearchHit } from '../lib/search'

/**
 * Pretraga po rijeci i po mjestu (UI.md §4).
 *
 * Rezultati su razdvojeni po tome šta jesu: dionica nosi stanje, mjerno mjesto ne nosi ništa
 * osim lokacije. Miješanje u jednu listu bi korisniku sugerisalo da su iste vrste stvari.
 */
export function Search({
  reaches,
  stations,
  onOpenReach,
  onOpenStation,
}: {
  reaches: ReachProperties[]
  stations: StationProperties[]
  onOpenReach: (key: string) => void
  onOpenStation: (key: string) => void
}) {
  const [query, setQuery] = useState('')
  const inputId = useId()

  const reachHits = useMemo(() => searchReaches(reaches, query), [reaches, query])
  const stationHits = useMemo(() => searchStations(stations, query), [stations, query])

  const open = (hit: SearchHit) =>
    hit.kind === 'reach' ? onOpenReach(hit.key) : onOpenStation(hit.key)

  return (
    <section aria-label="Pretraga">
      <label htmlFor={inputId} className="sr-only">
        Traži po imenu rijeke ili mjesta
      </label>
      <input
        id={inputId}
        type="search"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        placeholder="Rijeka ili mjesto — npr. Maglaj"
        autoComplete="off"
        className="w-full rounded border border-[--color-border] bg-[--color-surface] px-3 py-2 text-sm placeholder:text-[--color-text-muted]"
      />

      {query.trim().length > 0 && (
        <div className="mt-2" role="status" aria-live="polite">
          {reachHits.length === 0 && stationHits.length === 0 ? (
            /* Prazno stanje kaže šta je traženo i gdje je traženo (UI.md §7). */
            <p className="text-sm text-[--color-text-muted]">
              Ništa se ne poklapa sa „{query}”. Traži se po imenu dionice, rijeke, mjernog
              mjesta i opisu lokacije.
            </p>
          ) : (
            <>
              {reachHits.length > 0 && <Group title="Dionice" hits={reachHits} onOpen={open} />}
              {stationHits.length > 0 && (
                <Group title="Mjerna mjesta" hits={stationHits} onOpen={open} />
              )}
            </>
          )}
        </div>
      )}
    </section>
  )
}

function Group({
  title,
  hits,
  onOpen,
}: {
  title: string
  hits: SearchHit[]
  onOpen: (hit: SearchHit) => void
}) {
  return (
    <div className="mb-3">
      <h3 className="mb-1 text-xs font-semibold tracking-wide text-[--color-text-muted] uppercase">
        {title} ({hits.length})
      </h3>
      <ul>
        {hits.slice(0, 8).map((hit) => (
          <li key={`${hit.kind}-${hit.key}-${hit.title}`}>
            <button
              type="button"
              onClick={() => onOpen(hit)}
              className="w-full rounded px-2 py-1.5 text-left text-sm hover:bg-[--color-surface-raised]"
            >
              <span className="block">{hit.title}</span>
              {hit.subtitle && (
                <span className="block text-xs text-[--color-text-muted]">{hit.subtitle}</span>
              )}
            </button>
          </li>
        ))}
      </ul>
      {hits.length > 8 && (
        <p className="px-2 text-xs text-[--color-text-muted]">
          …i još {hits.length - 8}. Suzi pretragu.
        </p>
      )}
    </div>
  )
}
