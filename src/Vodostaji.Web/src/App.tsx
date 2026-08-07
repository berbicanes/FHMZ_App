import { useEffect, useMemo, useState } from 'react'
import { useQueries, useQuery } from '@tanstack/react-query'
import type {
  ReachCollection,
  ReachProperties,
  SourceStatus,
  StationCollection,
  StationProperties,
} from './api/types'
import { DisclaimerBar, PersistentDisclaimer } from './components/DisclaimerBar'
import { Coverage } from './components/Coverage'
import { Legend, PointSourceLegend } from './components/Legend'
import { ReachDetail } from './components/ReachDetail'
import { POINT_SOURCES, ReachMap, type PointSourceId } from './components/ReachMap'
import { ReachTable } from './components/ReachTable'
import { Search } from './components/Search'
import { StationDetail } from './components/StationDetail'
import { formatMeasuredAt } from './lib/freshness'
import { useRoute } from './lib/router'

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url)
  if (!response.ok) {
    throw new Error(`${url} je vratio ${response.status}.`)
  }
  return (await response.json()) as T
}

export default function App() {
  const [route, navigate] = useRoute()
  const [showStations, setShowStations] = useState(route.kind === 'station')
  // Mapa koja zakaže mora to reći. Prazna mapa bez poruke izgleda kao mapa bez opasnosti.
  const [mapError, setMapError] = useState<string | null>(null)

  // TanStack Query, nikad useEffect (CLAUDE.md → Konvencije).
  // Osvježavanje na 5 minuta; izvor se mijenja na sat, pa češće nema šta stići.
  const reaches = useQuery({
    queryKey: ['reaches'],
    queryFn: () => fetchJson<ReachCollection>('/api/v1/geojson/reaches'),
    refetchInterval: 5 * 60 * 1000,
  })

  // Registar se povlači kad je sloj uključen ili kad je otvoren deep link na stanicu —
  // link koji neko podijeli mora raditi i prije nego posjetilac išta uključi.
  const stations = useQuery({
    queryKey: ['stations'],
    queryFn: () => fetchJson<StationCollection>('/api/v1/geojson/stations'),
    enabled: showStations || route.kind === 'station',
    staleTime: 60 * 60 * 1000,
  })

  // Jedan upit po tačkastom izvoru. Zasebni slojevi, zasebni odgovori.
  const pointQueries = useQueries({
    queries: POINT_SOURCES.map((id) => ({
      queryKey: ['points', id],
      queryFn: () => fetchJson<ReachCollection>(`/api/v1/geojson/points/${id}`),
      refetchInterval: 5 * 60 * 1000,
    })),
  })

  const points = useMemo(() => {
    const map: Partial<Record<PointSourceId, ReachCollection>> = {}
    POINT_SOURCES.forEach((id, index) => {
      const data = pointQueries[index]?.data
      if (data) map[id] = data
    })
    return map
  }, [pointQueries])

  const sources = useQuery({
    queryKey: ['sources'],
    queryFn: () => fetchJson<SourceStatus[]>('/api/v1/sources'),
    refetchInterval: 5 * 60 * 1000,
  })

  // Dionice i tačke Jadrana idu u istu listu za pretragu i tabelu, ali **nikad u isti
  // sloj na mapi**. Korisnik iz Mostara traži "Mostar" i mora ga naći; to nije stapanje
  // legendi nego jedan indeks nad dva sloja.
  const reachProperties = useMemo(
    () => [
      ...(reaches.data?.features.map((f) => f.properties) ?? []),
      ...POINT_SOURCES.flatMap((id) => points[id]?.features.map((f) => f.properties) ?? []),
    ],
    [reaches.data, points],
  )
  const stationProperties = useMemo(
    () => stations.data?.features.map((f) => f.properties) ?? [],
    [stations.data],
  )

  // Šta je otvoreno određuje URL, ne stanje komponente. Tako podijeljen link uvijek
  // otvara isto što je pošiljalac gledao (UI.md §4).
  const selectedReach: ReachProperties | null =
    route.kind === 'reach'
      ? (reachProperties.find(
          (r) => r.stationKey === route.key && r.sourceId === route.sourceId,
        ) ?? null)
      : null

  const selectedStation: StationProperties | null =
    route.kind === 'station'
      ? (stationProperties.find(
          (s) => s.stationKey === route.key && s.sourceId === route.sourceId,
        ) ?? null)
      : null

  // Naslov kartice prati ono što je otvoreno — link dijeljen u poruci nosi ime dionice.
  useEffect(() => {
    const name = selectedReach?.name ?? selectedStation?.name
    document.title = name ? `${name} — Vodostaji BiH` : 'Vodostaji BiH'
  }, [selectedReach, selectedStation])

  const meta = reaches.data?.meta

  // Ruta koja pokazuje na nešto što ne postoji nije prazan ekran nego objašnjenje.
  const missingTarget =
    (route.kind === 'reach' && reaches.isSuccess && !selectedReach) ||
    (route.kind === 'station' && stations.isSuccess && !selectedStation)

  return (
    <div className="flex h-full flex-col">
      <DisclaimerBar />

      <header className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 border-b border-[--color-border] px-4 py-3">
        <button
          type="button"
          onClick={() => navigate({ kind: 'map' })}
          className="text-lg font-semibold"
        >
          Vodostaji BiH
        </button>
        {meta && (
          <p className="text-xs text-[--color-text-muted]">
            {meta.measuredCount} od {meta.reachCount} dionica ima podatak · povučeno{' '}
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

          <ReachMap
            data={reaches.data}
            points={points}
            stations={stations.data}
            showStations={showStations || route.kind === 'station'}
            onSelect={(reach) =>
              navigate({
                kind: 'reach',
                sourceId: reach.sourceId ?? '',
                key: reach.stationKey ?? '',
              })
            }
            onSelectStation={(station) =>
              navigate({
                kind: 'station',
                sourceId: station.sourceId ?? '',
                key: station.stationKey ?? '',
              })
            }
            onError={setMapError}
          />

          {mapError && (
            <div className="absolute inset-x-4 bottom-4 z-10 rounded border border-[#7a2020] bg-[#2a1010] p-3 text-sm">
              <p>
                Mapa se ne prikazuje ispravno: {mapError} Podaci su i dalje tačni u tabeli
                ispod.
              </p>
              <button
                type="button"
                onClick={() => setMapError(null)}
                className="mt-2 rounded border border-[#7a2020] px-2 py-1 text-xs"
              >
                Sakrij
              </button>
            </div>
          )}

          <div className="absolute top-3 left-3 z-10 rounded border border-[--color-border] bg-[--color-surface-raised]/95 px-3 py-2 text-sm">
            <label className="flex cursor-pointer items-center gap-2">
              <input
                type="checkbox"
                checked={showStations || route.kind === 'station'}
                onChange={(event) => setShowStations(event.target.checked)}
              />
              <span>Mjerna mjesta</span>
              {stations.data && (
                <span className="text-xs text-[--color-text-muted]">
                  ({stations.data.features.length})
                </span>
              )}
            </label>
          </div>
        </main>

        <div className="flex w-full flex-col overflow-y-auto border-t border-[--color-border] lg:w-[26rem] lg:border-t-0 lg:border-l">
          {missingTarget && (
            <div className="border-b border-[--color-border] bg-[--color-surface-raised] p-4 text-sm">
              <p>
                {route.kind === 'reach' ? 'Dionica' : 'Mjerno mjesto'} sa oznakom „{route.key}”
                ne postoji u trenutnim podacima. Možda je izvor promijenio oznake.
              </p>
              <button
                type="button"
                onClick={() => navigate({ kind: 'map' })}
                className="mt-2 rounded border border-[--color-border] px-2 py-1 text-xs"
              >
                Nazad na mapu
              </button>
            </div>
          )}

          {selectedReach && (
            <ReachDetail reach={selectedReach} onClose={() => navigate({ kind: 'map' })} />
          )}

          {selectedStation && (
            <StationDetail
              station={selectedStation}
              onClose={() => navigate({ kind: 'map' })}
            />
          )}

          <div className="space-y-5 p-4">
            <Search
              reaches={reachProperties}
              stations={stationProperties}
              onOpenReach={(sourceId, key) => navigate({ kind: 'reach', sourceId, key })}
              onOpenStation={(sourceId, key) => {
                setShowStations(true)
                navigate({ kind: 'station', sourceId, key })
              }}
            />

            {sources.data?.map((s) =>
              s.sourceId === 'avp-sava' ? (
                <Legend key={s.sourceId} agencyName={s.agencyName ?? 'izvor'} />
              ) : (
                <PointSourceLegend
                  key={s.sourceId}
                  agencyName={s.agencyName ?? 'izvor'}
                  color={s.sourceId === 'avpjm' ? '#4a8fd4' : '#3aa8a0'}
                  ring={s.sourceId !== 'avpjm'}
                  note={
                    s.sourceId === 'avpjm'
                      ? 'Ova agencija objavljuje vodostaj, ali ne i ocjenu opasnosti — bojenje po pragovima im je na sajtu dostupno samo prijavljenim korisnicima. Prikazujemo brojeve i pragove onako kako ih objavljuju, bez vlastite ocjene.'
                      : 'Ova agencija objavljuje vodostaj i trend, ali ne i ocjenu opasnosti po stanici. Trend prikazujemo onako kako ga ona objavi, a ne izvodimo ga sami.'
                  }
                />
              ),
            )}

            {sources.data && sources.data.length > 0 && (
              <section aria-label="Stanje izvora" className="text-xs text-[--color-text-muted]">
                <h2 className="mb-1 font-semibold tracking-wide uppercase">Izvori</h2>
                <ul className="space-y-2">
                  {sources.data.map((s) => (
                    <li key={s.sourceId}>
                      <p>
                        {s.agencyName} ·{' '}
                        {s.isHealthy
                          ? 'dostupan'
                          : `nedostupan (${s.lastFailureReason ?? '—'})`}
                      </p>
                      {/* Razlika između "mjeri" i "ocjenjuje" je cijela priča o ovom izvoru. */}
                      <p>
                        {s.measuredCount} mjerenja ·{' '}
                        {s.knownCount === 0
                          ? 'bez ocjene opasnosti'
                          : `${s.knownCount} sa ocjenom`}
                      </p>
                      {s.clockEvidence && (
                        <details className="mt-1">
                          <summary className="cursor-pointer">O vremenu mjerenja</summary>
                          <p className="mt-1 leading-relaxed">{s.clockEvidence}</p>
                        </details>
                      )}
                    </li>
                  ))}
                </ul>
              </section>
            )}

            {/* Registar mora objasniti razliku između broja stanica i broja tačaka na mapi,
                inače korisnik koji broji dobije drugi rezultat od nas. */}
            {stations.data && (
              <section
                aria-label="Registar mjernih mjesta"
                className="text-xs text-[--color-text-muted]"
              >
                <h2 className="mb-1 font-semibold tracking-wide uppercase">Mjerna mjesta</h2>
                <p className="leading-relaxed">
                  Registar ima {stations.data.meta.stationCount} stanica; na mapi ih je{' '}
                  {stations.data.features.length}. Razlika su{' '}
                  {stations.data.meta.withoutGeometry} bez koordinata i{' '}
                  {stations.data.meta.withoutName} bez naziva. Prsten označava mjesto mjerenja,
                  ne stanje — stanje se čita na dionici.
                </p>
              </section>
            )}

            <Coverage />

            <PersistentDisclaimer />
          </div>

          {/* Tabelarna alternativa mapi (UI.md §5) — ravnopravan prikaz, ne rezerva. */}
          <section aria-label="Tabelarni pregled" className="border-t border-[--color-border] p-4">
            <h2 className="mb-2 text-xs font-semibold tracking-wide text-[--color-text-muted] uppercase">
              Sve dionice
            </h2>
            <ReachTable
              reaches={reachProperties}
              onSelect={(reach) =>
              navigate({
                kind: 'reach',
                sourceId: reach.sourceId ?? '',
                key: reach.stationKey ?? '',
              })
            }
            />
          </section>
        </div>
      </div>
    </div>
  )
}
