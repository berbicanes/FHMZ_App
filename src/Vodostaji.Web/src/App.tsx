import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import type { ReachCollection, ReachProperties, SourceStatus } from './api/types'
import { DisclaimerBar, PersistentDisclaimer } from './components/DisclaimerBar'
import { Legend } from './components/Legend'
import { ReachDetail } from './components/ReachDetail'
import { ReachMap } from './components/ReachMap'
import { ReachTable } from './components/ReachTable'
import { formatMeasuredAt } from './lib/freshness'

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url)
  if (!response.ok) {
    throw new Error(`${url} je vratio ${response.status}.`)
  }
  return (await response.json()) as T
}

export default function App() {
  const [selected, setSelected] = useState<ReachProperties | null>(null)

  // TanStack Query, nikad useEffect (CLAUDE.md → Konvencije).
  // Osvježavanje na 5 minuta; izvor se mijenja na sat, pa češće nema šta stići.
  const reaches = useQuery({
    queryKey: ['reaches'],
    queryFn: () => fetchJson<ReachCollection>('/api/v1/geojson/reaches'),
    refetchInterval: 5 * 60 * 1000,
  })

  const sources = useQuery({
    queryKey: ['sources'],
    queryFn: () => fetchJson<SourceStatus[]>('/api/v1/sources'),
    refetchInterval: 5 * 60 * 1000,
  })

  const properties = reaches.data?.features.map((feature) => feature.properties) ?? []
  const meta = reaches.data?.meta
  const source = sources.data?.[0]

  return (
    <div className="flex h-full flex-col">
      <DisclaimerBar />

      <header className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 border-b border-[--color-border] px-4 py-3">
        <h1 className="text-lg font-semibold">Vodostaji BiH</h1>
        {meta && (
          <p className="text-xs text-[--color-text-muted]">
            {/* Vidljiv timestamp mjerenja, ne vremena dohvata (zlatno pravilo 2). */}
            {meta.knownCount} od {meta.reachCount} dionica ima podatak · povučeno{' '}
            {formatMeasuredAt(meta.fetchedAt) ?? '—'}
          </p>
        )}
      </header>

      <div className="flex min-h-0 flex-1 flex-col lg:flex-row">
        <main className="relative min-h-[55vh] flex-1 lg:min-h-0">
          {reaches.isPending && (
            <p className="absolute inset-0 z-10 flex items-center justify-center text-sm text-[--color-text-muted]">
              Učitavanje stanja rijeka…
            </p>
          )}

          {reaches.isError && (
            /* Greška kaže šta se desilo i ne izvinjava se (UI.md §7). */
            <div className="absolute inset-x-4 top-4 z-10 rounded border border-[#7a2020] bg-[#2a1010] p-3 text-sm">
              <p>Stanje rijeka se ne može učitati. {(reaches.error as Error).message}</p>
              <button
                type="button"
                onClick={() => reaches.refetch()}
                className="mt-2 rounded border border-[#7a2020] px-2 py-1 text-xs"
              >
                Pokušaj ponovo
              </button>
            </div>
          )}

          <ReachMap data={reaches.data} onSelect={setSelected} />
        </main>

        <div className="flex w-full flex-col overflow-y-auto border-t border-[--color-border] lg:w-[26rem] lg:border-t-0 lg:border-l">
          {selected && (
            <ReachDetail reach={selected} onClose={() => setSelected(null)} />
          )}

          <div className="space-y-5 p-4">
            {source && <Legend agencyName={source.agencyName ?? 'izvor'} />}

            {source && (
              <section aria-label="Stanje izvora" className="text-xs text-[--color-text-muted]">
                <h2 className="mb-1 font-semibold tracking-wide uppercase">Izvor</h2>
                <p>
                  {source.agencyName} ·{' '}
                  {source.isHealthy ? 'dostupan' : `nedostupan (${source.lastFailureReason ?? '—'})`}
                </p>
                {/* Pretpostavka o vremenskoj zoni je javno provjerljiva, ne skrivena. */}
                {source.clockEvidence && (
                  <details className="mt-1">
                    <summary className="cursor-pointer">O vremenu mjerenja</summary>
                    <p className="mt-1 leading-relaxed">{source.clockEvidence}</p>
                  </details>
                )}
              </section>
            )}

            <PersistentDisclaimer />
          </div>

          {/* Tabelarna alternativa mapi (UI.md §5) — ravnopravan prikaz, ne rezerva. */}
          <section aria-label="Tabelarni pregled" className="border-t border-[--color-border] p-4">
            <h2 className="mb-2 text-xs font-semibold tracking-wide text-[--color-text-muted] uppercase">
              Sve dionice
            </h2>
            <ReachTable reaches={properties} onSelect={setSelected} />
          </section>
        </div>
      </div>
    </div>
  )
}
