import { useMemo, useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  Area,
  AreaChart,
  CartesianGrid,
  ReferenceArea,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { ReachHistory, ReachThreshold } from '../api/types'
import { cm, seriesSummary } from '../lib/derived'

type Range = 7 | 30

interface Point {
  t: number
  value: number
}

/**
 * Historija vodostaja (UI.md §3).
 *
 * Pragovi se crtaju kao **pojasevi**, ne kao linije sa sitnim natpisom. Pojas pokazuje u
 * kojem se rasponu voda kreće u odnosu na ono što je agencija označila, a to je ono što se
 * čita jednim pogledom. Boje pojaseva su prigušene verzije njihove skale — dovoljne da se
 * raspoznaju, pretihe da se takmiče sa trenutnim stanjem.
 *
 * Prekid u podacima ostaje prekid: `connectNulls` je isključen. Linija koja glatko pređe
 * preko sata bez mjerenja tvrdi da znamo nešto što ne znamo.
 */
export function HistoryChart({
  sourceId,
  stationKey,
}: {
  sourceId: string
  stationKey: string
}) {
  const [days, setDays] = useState<Range>(7)

  const history = useQuery({
    // Ključ ide uz izvor. Bez toga graf jedne rijeke završi pod imenom druge.
    queryKey: ['history', sourceId, stationKey, days],
    queryFn: async () => {
      const response = await fetch(
        `/api/v1/reaches/${encodeURIComponent(sourceId)}/${encodeURIComponent(stationKey)}/history?days=${days}`,
      )
      if (!response.ok) throw new Error(`Historija nije dostupna (${response.status}).`)
      return (await response.json()) as ReachHistory
    },
    staleTime: 5 * 60 * 1000,
  })

  const points = useMemo<Point[]>(
    () =>
      (history.data?.points ?? [])
        .map((p) => ({ t: new Date(p.measuredAt ?? '').getTime(), value: p.valueCm ?? 0 }))
        .filter((p) => Number.isFinite(p.t)),
    [history.data],
  )

  const thresholds = useMemo(
    () =>
      (history.data?.thresholds ?? [])
        .filter((t): t is ReachThreshold & { valueCm: number } => typeof t.valueCm === 'number')
        .sort((a, b) => a.valueCm - b.valueCm),
    [history.data],
  )

  const domain = useMemo(() => {
    if (points.length === 0) return null
    const values = points.map((p) => p.value)
    const low = Math.min(...values, ...thresholds.map((t) => t.valueCm))
    const high = Math.max(...values, ...thresholds.map((t) => t.valueCm))
    const pad = Math.max((high - low) * 0.12, 5)
    return { min: low - pad, max: high + pad }
  }, [points, thresholds])

  const last = points.at(-1)
  const summary = useMemo(() => seriesSummary(points.map((p) => p.value)), [points])

  return (
    <section className="mt-5" aria-label="Historija vodostaja">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h3 className="eyebrow">
          Historija
          {summary && (
            <span className="ml-2 font-normal normal-case tracking-normal text-fg-muted">
              {summary.count} {summary.count === 1 ? 'očitanje' : 'očitanja'}
            </span>
          )}
        </h3>

        <div
          role="group"
          aria-label="Raspon grafa"
          className="flex rounded-chip border border-line bg-ink-800 p-0.5"
        >
          {([7, 30] as const).map((option) => (
            <button
              key={option}
              type="button"
              onClick={() => setDays(option)}
              aria-pressed={days === option}
              className={`rounded-chip px-2.5 py-1 text-xs transition-colors ${
                days === option
                  ? 'bg-ink-600 text-fg'
                  : 'text-fg-muted hover:text-fg-soft'
              }`}
            >
              {option} dana
            </button>
          ))}
        </div>
      </div>

      {history.isPending && (
        <div className="h-[300px] animate-pulse rounded-card bg-ink-800 xl:h-[360px]" />
      )}

      {history.isError && (
        <p className="text-sm text-fg-muted">{(history.error as Error).message}</p>
      )}

      {history.data && points.length === 0 && (
        /* Prazan graf mora reći zašto je prazan. „Tek smo počeli skupljati" i „izvor je pao"
           izgledaju identično na ekranu (UI.md §7). */
        <p className="rounded-card border border-line bg-ink-850 px-3 py-3 text-sm leading-relaxed text-fg-muted">
          {history.data.collectingSince
            ? `Za posljednjih ${days} dana nema zapisa. Historiju za ovu dionicu skupljamo od ${new Date(
                history.data.collectingSince,
              ).toLocaleDateString('bs-BA')}.`
            : 'Historiju tek počinjemo skupljati. Agencija ne objavljuje arhivu, pa graf raste od trenutka kad smo prvi put povukli podatke.'}
        </p>
      )}

      {history.data && points.length > 0 && domain && (
        <>
          {points.length < 4 && (
            <p className="mb-2 text-xs leading-relaxed text-fg-muted">
              Zasad {points.length} {points.length === 1 ? 'očitanje' : 'očitanja'} — premalo za
              oblik krivulje. Agencija ne objavljuje arhivu, pa graf raste iz onoga što skupimo.
            </p>
          )}

          {/* Graf probija unutrašnji razmak ploče — u koloni od 27rem svaki piksel
              širine je razlika između krivulje koja se čita i one koja se nazire. */}
          <div className="-mx-4 h-[300px] w-[calc(100%+2rem)] xl:h-[360px]">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={points} margin={{ top: 10, right: 16, bottom: 0, left: -8 }}>
                <defs>
                  <linearGradient id="waterFill" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#7fb4e8" stopOpacity={0.28} />
                    <stop offset="100%" stopColor="#7fb4e8" stopOpacity={0.02} />
                  </linearGradient>
                </defs>

                {/* Pojasevi između pragova. Prigušeni — pokazuju raspon, ne ocjenu. */}
                {thresholds.map((threshold, index) => {
                  const next = thresholds[index + 1]
                  return (
                    <ReferenceArea
                      key={`band-${threshold.label}`}
                      y1={threshold.valueCm}
                      y2={next ? next.valueCm : domain.max}
                      fill={BAND_COLORS[Math.min(index, BAND_COLORS.length - 1)]}
                      fillOpacity={1}
                      ifOverflow="hidden"
                      strokeWidth={0}
                    />
                  )
                })}

                {thresholds.map((threshold) => (
                  <ReferenceLine
                    key={threshold.label}
                    y={threshold.valueCm}
                    stroke="#4a5462"
                    strokeDasharray="3 4"
                    strokeWidth={1}
                  />
                ))}

                <CartesianGrid stroke="#232936" strokeDasharray="2 5" vertical={false} />

                <XAxis
                  dataKey="t"
                  type="number"
                  domain={['dataMin', 'dataMax']}
                  scale="time"
                  minTickGap={44}
                  tickFormatter={(value: number) =>
                    new Date(value).toLocaleDateString('bs-BA', {
                      day: 'numeric',
                      month: 'numeric',
                    })
                  }
                  stroke="#4a5462"
                  tick={{ fill: '#7c8798', fontSize: 11 }}
                  tickLine={false}
                  axisLine={{ stroke: '#232936' }}
                />

                <YAxis
                  domain={[domain.min, domain.max]}
                  width={52}
                  stroke="#4a5462"
                  tick={{ fill: '#7c8798', fontSize: 11 }}
                  tickLine={false}
                  axisLine={false}
                  tickFormatter={(value: number) => `${Math.round(value)}`}
                />

                <Tooltip
                  cursor={{ stroke: '#616b78', strokeWidth: 1 }}
                  contentStyle={{
                    background: '#12161d',
                    border: '1px solid #2a313d',
                    borderRadius: 10,
                    fontSize: 12,
                    boxShadow: '0 12px 32px -12px rgb(0 0 0 / 0.7)',
                  }}
                  labelStyle={{ color: '#7c8798', marginBottom: 2 }}
                  itemStyle={{ color: '#e9eef5' }}
                  labelFormatter={(value: number) =>
                    new Date(value).toLocaleString('bs-BA', {
                      day: 'numeric',
                      month: 'numeric',
                      hour: '2-digit',
                      minute: '2-digit',
                    })
                  }
                  formatter={(value: number) => [`${value} cm`, 'Vodostaj']}
                />

                <Area
                  type="monotone"
                  dataKey="value"
                  stroke="#8ec5ff"
                  strokeWidth={1.8}
                  fill="url(#waterFill)"
                  isAnimationActive={false}
                  connectNulls={false}
                  dot={points.length <= 12 ? { r: 2.5, fill: '#8ec5ff', strokeWidth: 0 } : false}
                  activeDot={{ r: 4, fill: '#e9eef5', strokeWidth: 0 }}
                />

                {/* Zadnje očitanje — jedina tačka koja uvijek ima oznaku. */}
                {last && (
                  <ReferenceLine
                    x={last.t}
                    stroke="#e9eef5"
                    strokeWidth={1}
                    strokeOpacity={0.35}
                  />
                )}
              </AreaChart>
            </ResponsiveContainer>
          </div>

          {/* Osa nosi samo brojeve; jedinica stoji jednom, ispod. */}
          <div className="mt-2 flex items-baseline justify-between text-xs text-fg-muted">
            <span>cm</span>
            {last && (
              <span className="tabular">
                zadnje: {cm(last.value)} cm ·{' '}
                {new Date(last.t).toLocaleString('bs-BA', {
                  day: 'numeric',
                  month: 'numeric',
                  hour: '2-digit',
                  minute: '2-digit',
                })}
              </span>
            )}
          </div>

          {/*
           * Sažetak prozora. Opis skupa brojeva, ne ocjena o njima — „najviše u 7 dana"
           * je činjenica o **našim zapisima**, i zato uz njega stoji koliko ih je uopšte.
           * Bez tog broja bi „najviše: 214 cm" iz tri očitanja izgledalo kao iz tri stotine.
           */}
          {summary && (
            <dl className="mt-3 grid grid-cols-3 gap-1.5">
              <Stat label="Najniže">{cm(summary.min)} cm</Stat>
              <Stat label="Najviše">{cm(summary.max)} cm</Stat>
              <Stat label="Raspon">{cm(summary.span)} cm</Stat>
            </dl>
          )}

          {thresholds.length > 0 && history.data.thresholdsDefinedBy && (
            // Prag bez imena onoga ko ga je postavio čita se kao naš (UI.md §3).
            <p className="mt-2 text-xs leading-relaxed text-fg-muted">
              Isprekidane linije su pragovi. Definiše ih {history.data.thresholdsDefinedBy}.
            </p>
          )}
        </>
      )}
    </section>
  )
}

/**
 * Pojasevi iznad svakog praga, u rastućoj ozbiljnosti.
 *
 * Ovo su **prigušene** verzije zvanične skale AVP Save — dovoljno da se raspoznaju, previše
 * tihe da bi se takmičile sa trenutnim stanjem na mapi ili sa samom krivuljom. Ne nose ocjenu;
 * pokazuju gdje su granice koje je agencija povukla.
 */
function Stat({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="rounded-card border border-line bg-ink-900 px-2.5 py-2">
      <dt className="eyebrow">{label}</dt>
      <dd className="numeric mt-0.5 text-sm font-semibold">{children}</dd>
    </div>
  )
}

const BAND_COLORS = [
  'rgb(56 168 0 / 0.07)',
  'rgb(255 255 0 / 0.07)',
  'rgb(255 170 0 / 0.09)',
  'rgb(230 0 0 / 0.11)',
]
