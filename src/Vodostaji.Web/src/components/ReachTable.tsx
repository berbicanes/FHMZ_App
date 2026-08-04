import type { ReachProperties } from '../api/types'
import { formatMeasuredAt, freshnessLabel, freshnessOf } from '../lib/freshness'

/**
 * Tabelarna alternativa mapi (UI.md §5).
 *
 * Nije rezervna varijanta nego ravnopravan prikaz: čitač ekrana i tastatura ovdje dobijaju
 * isto što i miš na mapi. Zato ide u DOM uvijek, ne iza toggle-a.
 */
export function ReachTable({
  reaches,
  onSelect,
}: {
  reaches: ReachProperties[]
  onSelect: (properties: ReachProperties) => void
}) {
  const sorted = [...reaches].sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '', 'bs'))

  return (
    <table className="w-full border-collapse text-sm">
      <caption className="sr-only">
        Stanje dionica rijeka, sa vremenom mjerenja i izvorom podatka.
      </caption>
      <thead>
        <tr className="border-b border-[--color-border] text-left text-xs text-[--color-text-muted] uppercase">
          <th scope="col" className="py-2 pr-3 font-semibold">Dionica</th>
          <th scope="col" className="py-2 pr-3 font-semibold">Stanje</th>
          <th scope="col" className="py-2 pr-3 text-right font-semibold">Vodostaj</th>
          <th scope="col" className="py-2 font-semibold">Mjereno</th>
        </tr>
      </thead>
      <tbody>
        {sorted.map((reach) => {
          const freshness = freshnessOf(reach)
          const measured = formatMeasuredAt(reach.measuredAt)

          return (
            <tr
              key={`${reach.sourceId}-${reach.stationKey}`}
              className="border-b border-[--color-border]/50 hover:bg-[--color-surface-raised]"
            >
              <th scope="row" className="py-2 pr-3 text-left font-normal">
                <button
                  type="button"
                  onClick={() => onSelect(reach)}
                  className="text-left hover:underline"
                >
                  {reach.name}
                </button>
              </th>

              <td className="py-2 pr-3">
                <span className="flex items-center gap-2">
                  {/* Boja ide uz tekst, nikad umjesto njega (UI.md §5). */}
                  <span
                    aria-hidden="true"
                    className="status-swatch h-2.5 w-2.5 shrink-0 rounded-sm border border-black/40"
                    style={{
                      backgroundColor: reach.color ?? '#cccccc',
                      backgroundImage:
                        freshness === 'unknown'
                          ? 'repeating-linear-gradient(45deg, #5c6470 0 2px, transparent 2px 5px)'
                          : undefined,
                    }}
                  />
                  <span>{reach.levelLabel}</span>
                </span>
              </td>

              <td className="tabular py-2 pr-3 text-right">
                {reach.valueCm === null || reach.valueCm === undefined ? (
                  <span className="text-[--color-text-muted]">—</span>
                ) : (
                  <>
                    {reach.valueCm} <span className="text-[--color-text-muted]">cm</span>
                  </>
                )}
              </td>

              <td className="py-2 text-[--color-text-muted]">
                {measured ? (
                  <>
                    <span>{measured}</span>
                    <span className="ml-1.5">({freshnessLabel(reach)})</span>
                  </>
                ) : (
                  /* Prazno stanje kaže šta se desilo, ne "ups" (UI.md §7). */
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
