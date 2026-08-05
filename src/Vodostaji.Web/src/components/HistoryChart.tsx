import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  CartesianGrid,
  Line,
  LineChart,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { ReachHistory } from '../api/types'

/**
 * Graf 7/30 dana (UI.md §3).
 *
 * Pragovi su horizontalne linije i **uz njih uvijek stoji ime agencije** koja ih je odredila.
 * Prag bez imena onoga ko ga je postavio čita se kao naš, a mi pragove ne određujemo.
 *
 * Bez interpolacije preko rupa: `connectNulls` je isključen, pa prekid u podacima ostaje
 * prekid. Linija koja glatko pređe preko sata u kojem mjerenja nema tvrdi da znamo nešto
 * što ne znamo.
 */
export function HistoryChart({ stationKey }: { stationKey: string }) {
  const [days, setDays] = useState<7 | 30>(7)

  const history = useQuery({
    queryKey: ['history', stationKey, days],
    queryFn: async () => {
      const response = await fetch(`/api/v1/reaches/${encodeURIComponent(stationKey)}/history?days=${days}`)
      if (!response.ok) throw new Error(`Historija nije dostupna (${response.status}).`)
      return (await response.json()) as ReachHistory
    },
    staleTime: 5 * 60 * 1000,
  })

  const points = (history.data?.points ?? []).map((point) => ({
    t: new Date(point.measuredAt ?? '').getTime(),
    value: point.valueCm,
  }))

  return (
    <section className="mt-4" aria-label="Historija vodostaja">
      <div className="mb-2 flex items-center justify-between">
        <h3 className="text-xs font-semibold tracking-wide text-[--color-text-muted] uppercase">
          Historija
        </h3>
        <div className="flex gap-1" role="group" aria-label="Raspon grafa">
          {([7, 30] as const).map((option) => (
            <button
              key={option}
              type="button"
              onClick={() => setDays(option)}
              aria-pressed={days === option}
              className={`rounded border px-2 py-0.5 text-xs ${
                days === option
                  ? 'border-[--color-text] text-[--color-text]'
                  : 'border-[--color-border] text-[--color-text-muted]'
              }`}
            >
              {option} dana
            </button>
          ))}
        </div>
      </div>

      {history.isPending && (
        <p className="text-sm text-[--color-text-muted]">Učitavanje historije…</p>
      )}

      {history.isError && (
        <p className="text-sm text-[--color-text-muted]">
          {(history.error as Error).message}
        </p>
      )}

      {history.data && points.length === 0 && (
        /* Prazan graf mora reći zašto je prazan. Tek smo počeli skupljati nije isto što i
           izvor je pao, a korisnik razliku ne može pogoditi (UI.md §7). */
        <p className="text-sm leading-relaxed text-[--color-text-muted]">
          {history.data.collectingSince
            ? `Za posljednjih ${days} dana nema zapisa. Historiju za ovu dionicu skupljamo od ${new Date(history.data.collectingSince).toLocaleDateString('bs-BA')}.`
            : 'Historiju za ovu dionicu tek počinjemo skupljati. Agencija ne objavljuje arhivu, pa graf raste od trenutka kad smo prvi put povukli podatke.'}
        </p>
      )}

      {history.data && points.length > 0 && (
        <>
          {points.length < 4 && (
            <p className="mb-2 text-xs leading-relaxed text-[--color-text-muted]">
              Zasad {points.length}{' '}
              {points.length === 1 ? 'očitanje' : points.length < 5 ? 'očitanja' : 'očitanja'} —
              premalo za oblik krivulje. Agencija ne objavljuje arhivu, pa graf raste iz onoga
              što skupimo.
            </p>
          )}

          <div className="h-44 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={points} margin={{ top: 6, right: 8, bottom: 4, left: -12 }}>
                <CartesianGrid stroke="#30363d" strokeDasharray="2 4" />
                <XAxis
                  dataKey="t"
                  type="number"
                  domain={['dataMin', 'dataMax']}
                  scale="time"
                  tickFormatter={(value: number) =>
                    new Date(value).toLocaleDateString('bs-BA', {
                      day: 'numeric',
                      month: 'numeric',
                    })
                  }
                  stroke="#9198a1"
                  fontSize={11}
                />
                <YAxis stroke="#9198a1" fontSize={11} width={44} unit=" cm" />

                {/* Pragovi agencije kao horizontalne linije. */}
                {history.data.thresholds?.map((threshold) => (
                  <ReferenceLine
                    key={threshold.label}
                    y={threshold.valueCm}
                    stroke="#9198a1"
                    strokeDasharray="4 4"
                    label={{
                      value: threshold.label ?? '',
                      position: 'insideTopLeft',
                      fill: '#9198a1',
                      fontSize: 10,
                    }}
                  />
                ))}

                <Tooltip
                  contentStyle={{
                    background: '#161b22',
                    border: '1px solid #30363d',
                    fontSize: 12,
                  }}
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

                <Line
                  type="linear"
                  dataKey="value"
                  stroke="#e6edf3"
                  strokeWidth={1.6}
                  dot={{ r: 2 }}
                  isAnimationActive={false}
                  connectNulls={false}
                />
              </LineChart>
            </ResponsiveContainer>
          </div>

          {history.data.thresholdsDefinedBy && (
            <p className="mt-1 text-xs text-[--color-text-muted]">
              Pragove definiše {history.data.thresholdsDefinedBy}.
            </p>
          )}
        </>
      )}
    </section>
  )
}
