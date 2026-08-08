import { useEffect, useMemo, useState } from 'react'
import { useQueries, useQuery } from '@tanstack/react-query'
import type {
  ReachCollection,
  ReachProperties,
  SourceStatus,
  StationCollection,
  StationProperties,
} from './api/types'
import { Coverage } from './components/Coverage'
import { DisclaimerBar, PersistentDisclaimer } from './components/DisclaimerBar'
import { PointSourceLegend, SavaLegend } from './components/Legend'
import { Section } from './components/Panel'
import { ReachDetail } from './components/ReachDetail'
import { POINT_SOURCES, ReachMap, type PointSourceId } from './components/ReachMap'
import { ReachTable } from './components/ReachTable'
import { Search } from './components/Search'
import { StationDetail } from './components/StationDetail'
import { StatusFilter } from './components/StatusFilter'
import { formatMeasuredAt } from './lib/freshness'
import { bucketOf, summarizeBuckets, type BucketKey } from './lib/levels'
import { useRoute } from './lib/router'

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url)
  if (!response.ok) throw new Error(`${url} je vratio ${response.status}.`)
  return (await response.json()) as T
}

const SOURCE_COLOR: Record<string, string> = {
  avpjm: '#4a8fd4',
  fhmzbih: '#3aa8a0',
}

const SOURCE_NOTE: Record<string, string> = {
  avpjm:
    'Ova agencija objavljuje vodostaj, ali ne i ocjenu opasnosti — bojenje po pragovima im je na sajtu dostupno samo prijavljenim korisnicima. Prikazujemo brojeve i pragove onako kako ih objavljuju, bez vlastite ocjene.',
  fhmzbih:
    'Ova agencija objavljuje vodostaj i trend, ali ne i ocjenu opasnosti po stanici. Trend prikazujemo onako kako ga ona objavi, a ne izvodimo ga sami.',
}

export default function App() {
  const [route, navigate] = useRoute()
  const [showStations, setShowStations] = useState(route.kind === 'station')
  const [showLabels, setShowLabels] = useState(false)
  /** `null` znači „sve prikazano". Prazan skup se ne dopušta — bio bi prazna mapa bez razloga. */
  const [buckets, setBuckets] = useState<BucketKey[] | null>(null)
  // Mapa koja zakaže mora to reći. Prazna mapa bez poruke izgleda kao mapa bez opasnosti.
  const [mapError, setMapError] = useState<string | null>(null)

  // TanStack Query, nikad useEffect (CLAUDE.md → Konvencije).
  const reaches = useQuery({
    queryKey: ['reaches'],
    queryFn: () => fetchJson<ReachCollection>('/api/v1/geojson/reaches'),
    refetchInterval: 5 * 60 * 1000,
  })

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

  // Registar se povlači kad je sloj uključen ili kad je otvoren deep link na stanicu —
  // link koji neko podijeli mora raditi i prije nego posjetilac išta uključi.
  const stations = useQuery({
    queryKey: ['stations'],
    queryFn: () => fetchJson<StationCollection>('/api/v1/geojson/stations'),
    enabled: showStations || route.kind === 'station',
    staleTime: 60 * 60 * 1000,
  })

  const sources = useQuery({
    queryKey: ['sources'],
    queryFn: () => fetchJson<SourceStatus[]>('/api/v1/sources'),
    refetchInterval: 5 * 60 * 1000,
  })

  // Dionice i tačke idu u istu listu za pretragu i tabelu, ali **nikad u isti sloj na
  // mapi**. Korisnik iz Mostara traži „Mostar" i mora ga naći; to je jedan indeks nad tri
  // sloja, ne jedna legenda nad tri agencije.
  const allReaches = useMemo(
    () => [
      ...(reaches.data?.features.map((f) => f.properties) ?? []),
      ...POINT_SOURCES.flatMap((id) => points[id]?.features.map((f) => f.properties) ?? []),
    ],
    [reaches.data, points],
  )

  // Traka sa brojevima uvijek broji **sve**, i ono što je filter sakrio. Filter koji krije
  // i vlastiti broj ostavlja utisak da nečega nema.
  const summary = useMemo(() => summarizeBuckets(allReaches), [allReaches])

  const visibleReaches = useMemo(
    () => (buckets === null ? allReaches : allReaches.filter((r) => buckets.includes(bucketOf(r)))),
    [allReaches, buckets],
  )

  const stationProperties = useMemo(
    () => stations.data?.features.map((f) => f.properties) ?? [],
    [stations.data],
  )

  // Šta je otvoreno određuje URL, ne stanje komponente (UI.md §4).
  const selectedReach: ReachProperties | null =
    route.kind === 'reach'
      ? (allReaches.find(
          (r) => r.stationKey === route.key && r.sourceId === route.sourceId,
        ) ?? null)
      : null

  const selectedStation: StationProperties | null =
    route.kind === 'station'
      ? (stationProperties.find(
          (s) => s.stationKey === route.key && s.sourceId === route.sourceId,
        ) ?? null)
      : null

  useEffect(() => {
    const name = selectedReach?.name ?? selectedStation?.name
    document.title = name ? `${name} — Vodostaji BiH` : 'Vodostaji BiH'
  }, [selectedReach, selectedStation])

  const meta = reaches.data?.meta
  const totalMeasured = useMemo(
    () => allReaches.filter((r) => r.valueCm !== null && r.valueCm !== undefined).length,
    [allReaches],
  )

  const missingTarget =
    (route.kind === 'reach' && reaches.isSuccess && !selectedReach) ||
    (route.kind === 'station' && stations.isSuccess && !selectedStation)

  const openReach = (reach: ReachProperties) =>
    navigate({ kind: 'reach', sourceId: reach.sourceId ?? '', key: reach.stationKey ?? '' })

  /**
   * Ponašanje trake: prvi klik izdvaja grupu, sljedeći je vraća.
   *
   * Zadnja upaljena grupa se ne da ugasiti — vratila bi praznu mapu bez ijedne oznake, što
   * izgleda identično kao pad izvora. Umjesto toga se filter poništava.
   */
  const toggleBucket = (key: BucketKey) =>
    setBuckets((current) => {
      if (current === null) return [key]
      if (!current.includes(key)) return [...current, key]
      if (current.length === 1) return null
      return current.filter((value) => value !== key)
    })

  const detail = selectedReach ?? selectedStation

  return (
    <div className="flex h-full flex-col bg-ink-950">
      <DisclaimerBar />

      <div className="relative flex min-h-0 flex-1 flex-col lg:flex-row">
        {/* Mapa je podloga cijelog ekrana; ploče plutaju nad njom. */}
        <main className="relative min-h-[45vh] flex-1 lg:min-h-0">
          <ReachMap
            data={reaches.data}
            points={points}
            stations={stations.data}
            showStations={showStations || route.kind === 'station'}
            showLabels={showLabels}
            bucketFilter={buckets}
            selected={
              selectedReach
                ? {
                    sourceId: selectedReach.sourceId ?? '',
                    key: selectedReach.stationKey ?? '',
                  }
                : null
            }
            onSelect={openReach}
            onSelectStation={(station) =>
              navigate({
                kind: 'station',
                sourceId: station.sourceId ?? '',
                key: station.stationKey ?? '',
              })
            }
            onError={setMapError}
          />

          {/* Zaglavlje pluta nad mapom umjesto da joj oduzima visinu. */}
          <header className="panel absolute top-3 left-3 z-10 px-3.5 py-2.5">
            <button type="button" onClick={() => navigate({ kind: 'map' })} className="text-left">
              <span className="block text-sm leading-tight font-semibold">Vodostaji BiH</span>
              <span className="block text-[11px] leading-tight text-fg-muted">
                {totalMeasured > 0
                  ? `${totalMeasured} mjerenja · ${formatMeasuredAt(meta?.fetchedAt) ?? '—'}`
                  : 'Učitavanje…'}
              </span>
            </button>
          </header>

          {/* Prekidači mape na jednom mjestu, u donjem lijevom uglu — dalje od kontrola
              zuma desno, i dalje od trake sa atribucijom. */}
          <div className="panel absolute bottom-3 left-3 z-10 space-y-1.5 px-3.5 py-2.5 text-sm">
            <label className="flex cursor-pointer items-center gap-2.5">
              <input
                type="checkbox"
                checked={showStations || route.kind === 'station'}
                onChange={(event) => setShowStations(event.target.checked)}
                className="accent-fg-soft"
              />
              <span>Mjerna mjesta</span>
              {stations.data && (
                <span className="tabular text-xs text-fg-muted">
                  {stations.data.features.length}
                </span>
              )}
            </label>

            <label className="flex cursor-pointer items-center gap-2.5">
              <input
                type="checkbox"
                checked={showLabels}
                onChange={(event) => setShowLabels(event.target.checked)}
                className="accent-fg-soft"
              />
              <span>Imena i vrijednosti</span>
            </label>
          </div>

          {reaches.isPending && (
            <p className="panel absolute top-1/2 left-1/2 z-10 -translate-x-1/2 -translate-y-1/2 px-4 py-3 text-sm text-fg-soft">
              Učitavanje stanja rijeka…
            </p>
          )}

          {reaches.isError && (
            /* Greška kaže šta se desilo i ne izvinjava se (UI.md §7). */
            <div className="absolute inset-x-3 top-20 z-10 rounded-panel border border-[#7a2020] bg-[#2a1010] p-3 text-sm">
              <p>Stanje rijeka se ne može učitati. {(reaches.error as Error).message}</p>
              <button
                type="button"
                onClick={() => reaches.refetch()}
                className="mt-2 rounded-chip border border-[#7a2020] px-3 py-1 text-xs"
              >
                Pokušaj ponovo
              </button>
            </div>
          )}

          {mapError && (
            <div className="absolute inset-x-3 bottom-24 z-10 rounded-panel border border-[#7a2020] bg-[#2a1010] p-3 text-sm">
              <p>
                Mapa se ne prikazuje ispravno: {mapError} Podaci su i dalje tačni u listi sa
                strane.
              </p>
              <button
                type="button"
                onClick={() => setMapError(null)}
                className="mt-2 rounded-chip border border-[#7a2020] px-3 py-1 text-xs"
              >
                Sakrij
              </button>
            </div>
          )}
        </main>

        <aside className="relative flex min-h-0 flex-1 flex-col border-t border-line bg-ink-900 lg:h-full lg:w-[30rem] lg:flex-none lg:border-t-0 lg:border-l xl:w-[34rem]">
          <div className="scroll-soft flex-1 overflow-y-auto">
            <div className="sticky top-0 z-10 border-b border-line bg-ink-900/95 p-4 backdrop-blur">
              <Search
                reaches={allReaches}
                stations={stationProperties}
                onOpenReach={(sourceId, key) => navigate({ kind: 'reach', sourceId, key })}
                onOpenStation={(sourceId, key) => {
                  setShowStations(true)
                  navigate({ kind: 'station', sourceId, key })
                }}
              />
            </div>

            <div className="border-b border-line">
              <StatusFilter
                buckets={summary}
                active={buckets}
                onToggle={toggleBucket}
                onReset={() => setBuckets(null)}
              />
            </div>

            <Section
              title={buckets === null ? 'Sve dionice' : 'Odabrane dionice'}
              badge={<Count value={visibleReaches.length} />}
            >
              <ReachTable
                reaches={visibleReaches}
                selectedKey={
                  selectedReach ? `${selectedReach.sourceId}-${selectedReach.stationKey}` : null
                }
                onSelect={openReach}
              />
            </Section>

            <Section title="Legenda" defaultOpen={false}>
              <div className="space-y-5">
                {sources.data?.map((source) =>
                  source.sourceId === 'avp-sava' ? (
                    <SavaLegend key={source.sourceId} agencyName={source.agencyName ?? 'izvor'} />
                  ) : (
                    <PointSourceLegend
                      key={source.sourceId}
                      agencyName={source.agencyName ?? 'izvor'}
                      color={SOURCE_COLOR[source.sourceId ?? ''] ?? '#cccccc'}
                      ring={source.sourceId !== 'avpjm'}
                      note={SOURCE_NOTE[source.sourceId ?? ''] ?? ''}
                    />
                  ),
                )}
              </div>
            </Section>

            <Section
              title="Izvori"
              defaultOpen={false}
              badge={<Count value={sources.data?.length ?? 0} />}
            >
              <ul className="space-y-3 text-xs text-fg-muted">
                {sources.data?.map((source) => (
                  <li key={source.sourceId}>
                    <p className="text-fg-soft">{source.agencyName}</p>
                    <p>
                      {source.isHealthy
                        ? 'dostupan'
                        : `nedostupan — ${source.lastFailureReason ?? '—'}`}{' '}
                      · {source.measuredCount} mjerenja ·{' '}
                      {/* Razlika između „mjeri" i „ocjenjuje" je cijela priča o izvoru. */}
                      {source.knownCount === 0
                        ? 'bez ocjene opasnosti'
                        : `${source.knownCount} sa ocjenom`}
                    </p>
                    {source.clockEvidence && (
                      <details className="mt-1">
                        <summary className="cursor-pointer">O vremenu mjerenja</summary>
                        <p className="mt-1 leading-relaxed">{source.clockEvidence}</p>
                      </details>
                    )}
                  </li>
                ))}
              </ul>

              {stations.data && (
                <p className="mt-3 text-xs leading-relaxed text-fg-muted">
                  Registar ima {stations.data.meta.stationCount} mjernih mjesta; na mapi ih je{' '}
                  {stations.data.features.length}. Razlika su{' '}
                  {stations.data.meta.withoutGeometry} bez koordinata i{' '}
                  {stations.data.meta.withoutName} bez naziva.
                </p>
              )}
            </Section>

            <Section title="Šta nije pokriveno" defaultOpen={false}>
              <Coverage />
            </Section>

            <div className="p-4">
              <PersistentDisclaimer />
            </div>
          </div>

          {missingTarget && (
            <div className="absolute inset-x-0 bottom-0 z-20 border-t border-line bg-ink-850 p-4 text-sm">
              <p>
                {route.kind === 'reach' ? 'Dionica' : 'Mjerno mjesto'} sa oznakom „{route.key}”
                ne postoji u trenutnim podacima. Možda je izvor promijenio oznake.
              </p>
              <button
                type="button"
                onClick={() => navigate({ kind: 'map' })}
                className="mt-2 rounded-chip border border-line-strong px-3 py-1 text-xs"
              >
                Nazad na pregled
              </button>
            </div>
          )}

          {/*
           * Detalj prekriva cijelu ploču umjesto da gura listu prema dolje.
           *
           * Ranije je stajao iznad tabele, pa je i graf i skala pragova morao stati u dio
           * kolone — a ispod je stajalo 77 redova koji su u tom trenutku nikome ne trebaju.
           * Ovako detalj dobija punu visinu, a lista ostaje tačno tamo gdje je bila kad se
           * zatvori.
           */}
          {detail && (
            <div className="scroll-soft absolute inset-0 z-30 overflow-y-auto bg-ink-850">
              {selectedReach ? (
                <ReachDetail reach={selectedReach} onClose={() => navigate({ kind: 'map' })} />
              ) : (
                selectedStation && (
                  <StationDetail
                    station={selectedStation}
                    onClose={() => navigate({ kind: 'map' })}
                  />
                )
              )}
            </div>
          )}
        </aside>
      </div>
    </div>
  )
}

function Count({ value }: { value: number }) {
  return (
    <span className="tabular rounded-chip bg-ink-800 px-2 py-0.5 text-[11px] text-fg-muted">
      {value}
    </span>
  )
}
