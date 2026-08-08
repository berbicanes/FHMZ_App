import { useId, useMemo, useState } from 'react'
import type { ReachProperties, StationProperties } from '../api/types'
import { searchReaches, searchStations, type SearchHit } from '../lib/search'

/**
 * Pretraga po rijeci i po mjestu (UI.md §4).
 *
 * Rezultati su razdvojeni po tome šta jesu: dionica nosi stanje, mjerno mjesto ne nosi ništa
 * osim lokacije. Miješanje u jednu listu sugerisalo bi da su iste vrste stvari.
 */
export function Search({
  reaches,
  stations,
  onOpenReach,
  onOpenStation,
}: {
  reaches: ReachProperties[]
  stations: StationProperties[]
  onOpenReach: (sourceId: string, key: string) => void
  onOpenStation: (sourceId: string, key: string) => void
}) {
  const [query, setQuery] = useState('')
  const inputId = useId()

  const reachHits = useMemo(() => searchReaches(reaches, query), [reaches, query])
  const stationHits = useMemo(() => searchStations(stations, query), [stations, query])

  const open = (hit: SearchHit) => {
    setQuery('')
    if (hit.kind === 'reach') onOpenReach(hit.sourceId, hit.key)
    else onOpenStation(hit.sourceId, hit.key)
  }

  return (
    <div>
      <label htmlFor={inputId} className="sr-only">
        Traži po imenu rijeke ili mjesta
      </label>

      <div className="relative">
        <svg
          width="15"
          height="15"
          viewBox="0 0 15 15"
          aria-hidden="true"
          className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-fg-muted"
        >
          <circle cx="6.5" cy="6.5" r="4.5" fill="none" stroke="currentColor" strokeWidth="1.5" />
          <path d="M10 10l4 4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        </svg>

        <input
          id={inputId}
          type="search"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Rijeka ili mjesto — npr. Maglaj"
          autoComplete="off"
          className="w-full rounded-card border border-line-strong bg-ink-850 py-2.5 pr-3 pl-9 text-sm placeholder:text-fg-muted focus:border-fg-soft"
        />
      </div>

      {query.trim().length > 0 && (
        <div className="mt-2" role="status" aria-live="polite">
          {reachHits.length === 0 && stationHits.length === 0 ? (
            /* Prazno stanje kaže šta je traženo i gdje je traženo (UI.md §7). */
            <p className="px-1 text-sm leading-relaxed text-fg-muted">
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
    </div>
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
    <div className="mb-3 last:mb-0">
      <p className="eyebrow mb-1 px-1">
        {title} · {hits.length}
      </p>
      <ul>
        {hits.slice(0, 8).map((hit) => (
          <li key={`${hit.kind}-${hit.sourceId}-${hit.key}`}>
            <button
              type="button"
              onClick={() => onOpen(hit)}
              className="w-full rounded-card px-2 py-1.5 text-left text-sm hover:bg-ink-800"
            >
              <span className="block">{hit.title}</span>
              {hit.subtitle && (
                <span className="block truncate text-xs text-fg-muted">
                  {hit.subtitle}
                </span>
              )}
            </button>
          </li>
        ))}
      </ul>
      {hits.length > 8 && (
        <p className="px-2 text-xs text-fg-muted">
          …i još {hits.length - 8}. Suzi pretragu.
        </p>
      )}
    </div>
  )
}
