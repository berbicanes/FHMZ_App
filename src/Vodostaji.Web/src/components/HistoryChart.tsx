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
  // Prazan string znači „vodostaj" — server na nepoznato ime i tako pada na njega.
  const [parameter, setParameter] = useState('WaterLevel')

  const history = useQuery({
    // Ključ ide uz izvor. Bez toga graf jedne rijeke završi pod imenom druge.
    queryKey: ['history', sourceId, stationKey, days, parameter],
    queryFn: async () => {
      const response = await fetch(
        `/api/v1/reaches/${encodeURIComponent(sourceId)}/${encodeURIComponent(stationKey)}/history?days=${days}&parameter=${encodeURIComponent(parameter)}`,
      )
      if (!response.ok) throw new Error(`Historija nije dostupna (${response.status}).`)
      return (await response.json()) as ReachHistory
    },
    staleTime: 5 * 60 * 1000,
  })

  const points = useMemo<Point[]>(
    () =>
      (history.data?.points ?? [])
        .map((p) => ({ t: new Date(p.measuredAt ?? '').getTime(), value: p.value ?? 0 }))
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
  const unit = history.data?.unit ?? 'cm'
  const label =
    history.data?.available?.find((a) => a.parameter === parameter)?.label ?? 'Vodostaj'
  const available = history.data?.available ?? []
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

      {/* Izbor parametra. Nudi se **samo ono što stanica stvarno ima u historiji**, pa
          nijedan izbor ne vodi u prazan graf. Kad je dostupan samo vodostaj, izbora nema. */}
      {available.length > 1 && (
        <div
          role="group"
          aria-label="Parametar grafa"
          className="mb-3 flex flex-wrap gap-1.5"
        >
          {available.map((option) => (
            <button
              key={option.parameter}
              type="button"
              onClick={() => setParameter(option.parameter ?? 'WaterLevel')}
              aria-pressed={parameter === option.parameter}
              className={`rounded-chip border px-2.5 py-1 text-xs transition-colors ${
                parameter === option.parameter
                  ? 'border-line-strong bg-ink-800 text-fg'
                  : 'border-line text-fg-muted hover:text-fg-soft'
              }`}
            >
              {option.label}
              <span className="ml-1 text-fg-muted">{option.unit}</span>
            </button>
          ))}
        </div>
      )}

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
                    <stop offset="0%" stopColor="#2f6fb0" stopOpacity={0.28} />
                    <stop offset="100%" stopColor="#2f6fb0" stopOpacity={0.02} />
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
                    stroke="#b8c2d0"
                    strokeDasharray="3 4"
                    strokeWidth={1}
                  />
                ))}

                <CartesianGrid stroke="#e2e8f1" strokeDasharray="2 5" vertical={false} />

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
                  stroke="#b8c2d0"
                  tick={{ fill: '#667487', fontSize: 11 }}
                  tickLine={false}
                  axisLine={{ stroke: '#e2e8f1' }}
                />

                <YAxis
                  domain={[domain.min, domain.max]}
                  width={52}
                  stroke="#b8c2d0"
                  tick={{ fill: '#667487', fontSize: 11 }}
                  tickLine={false}
                  axisLine={false}
                  tickFormatter={(value: number) => `${Math.round(value)}`}
                />

                <Tooltip
                  cursor={{ stroke: '#0b1018', strokeWidth: 1 }}
                  contentStyle={{
                    background: '#ffffff',
                    border: '1px solid #d4dce7',
                    borderRadius: 10,
                    fontSize: 12,
                    boxShadow: '0 12px 32px -12px rgb(0 0 0 / 0.7)',
                  }}
                  labelStyle={{ color: '#667487', marginBottom: 2 }}
                  itemStyle={{ color: '#0b1018' }}
                  labelFormatter={(value: number) =>
                    new Date(value).toLocaleString('bs-BA', {
                      day: 'numeric',
                      month: 'numeric',
                      hour: '2-digit',
                      minute: '2-digit',
                    })
                  }
                  formatter={(value: number) => [`${cm(value)} ${unit}`, label]}
                />

                <Area
                  type="monotone"
                  dataKey="value"
                  stroke="#2f6fb0"
                  strokeWidth={1.8}
                  fill="url(#waterFill)"
                  isAnimationActive={false}
                  connectNulls={false}
                  dot={points.length <= 12 ? { r: 2.5, fill: '#2f6fb0', strokeWidth: 0 } : false}
                  activeDot={{ r: 4, fill: '#0b1018', strokeWidth: 0 }}
                />

                {/* Zadnje očitanje — jedina tačka koja uvijek ima oznaku. */}
                {last && (
                  <ReferenceLine
                    x={last.t}
                    stroke="#0b1018"
                    strokeWidth={1}
                    strokeOpacity={0.35}
                  />
                )}
              </AreaChart>
            </ResponsiveContainer>
          </div>

          {/* Osa nosi samo brojeve; jedinica stoji jednom, ispod. */}
          <div className="mt-2 flex items-baseline justify-between text-xs text-fg-muted">
            <span>{unit}</span>
            {last && (
              <span className="tabular">
                zadnje: {cm(last.value)} {unit} ·{' '}
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
              <Stat label="Najniže">{cm(summary.min)} {unit}</Stat>
              <Stat label="Najviše">{cm(summary.max)} {unit}</Stat>
              <Stat label="Raspon">{cm(summary.span)} {unit}</Stat>
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
