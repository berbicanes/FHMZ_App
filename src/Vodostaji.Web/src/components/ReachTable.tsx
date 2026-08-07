import type { ReachProperties } from '../api/types'
import { formatMeasuredAt, freshnessLabel } from '../lib/freshness'
import { StatusDot } from './StatusMark'

/**
 * Tabelarna alternativa mapi (UI.md §5).
 *
 * Nije rezervna varijanta nego ravnopravan prikaz: čitač ekrana i tastatura ovdje dobijaju
 * isto što i miš na mapi. Zato je uvijek u DOM-u, i kad je sekcija sklopljena.
 */
export function ReachTable({
  reaches,
  selectedKey,
  onSelect,
}: {
  reaches: ReachProperties[]
  selectedKey: string | null
  onSelect: (reach: ReachProperties) => void
}) {
  const sorted = [...reaches].sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '', 'bs'))

  return (
    <table className="w-full border-collapse text-sm">
      <caption className="sr-only">
        Stanje dionica i mjernih mjesta, sa vremenom mjerenja i izvorom podatka.
      </caption>
      <thead>
        <tr className="border-b border-[--color-line] text-left">
          <th scope="col" className="eyebrow py-2 pr-3 font-semibold">
            Dionica
          </th>
          <th scope="col" className="eyebrow py-2 pr-3 text-right font-semibold">
            Vodostaj
          </th>
          <th scope="col" className="eyebrow py-2 font-semibold">
            Mjereno
          </th>
        </tr>
      </thead>
      <tbody>
        {sorted.map((reach) => {
          const key = `${reach.sourceId}-${reach.stationKey}`
          const measured = formatMeasuredAt(reach.measuredAt)
          const selected = key === selectedKey

          return (
            <tr
              key={key}
              className={`border-b border-[--color-line]/60 last:border-b-0 ${
                selected ? 'bg-[--color-ink-800]' : 'hover:bg-[--color-ink-850]'
              }`}
            >
              <th scope="row" className="py-2 pr-3 text-left font-normal">
                <button
                  type="button"
                  onClick={() => onSelect(reach)}
                  className="flex items-center gap-2 text-left"
                >
                  <StatusDot reach={reach} size={9} />
                  <span className="min-w-0">
                    <span className="block truncate">{reach.name}</span>
                    {/* Boja nikad sama — natpis stanja ide uz nju i u tabeli (UI.md §5). */}
                    <span className="block truncate text-xs text-[--color-text-muted]">
                      {reach.levelLabel}
                    </span>
                  </span>
                </button>
              </th>

              <td className="numeric py-2 pr-3 text-right whitespace-nowrap">
                {reach.valueCm === null || reach.valueCm === undefined ? (
                  <span className="font-sans text-[--color-text-muted]">—</span>
                ) : (
                  <>
                    <span className="font-semibold">{reach.valueCm}</span>{' '}
                    <span className="font-sans text-xs text-[--color-text-muted]">cm</span>
                  </>
                )}
              </td>

              <td className="py-2 text-xs text-[--color-text-muted]">
                {measured ? (
                  <>
                    <span className="block">{measured}</span>
                    <span className="block">{freshnessLabel(reach)}</span>
                  </>
                ) : (
                  /* Prazno stanje kaže šta se desilo, ne „ups" (UI.md §7). */
                  <span>{reach.noDataReason ?? 'Nema podatka'}</span>
                )}
              </td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}
