import { useMemo, useState } from 'react'
import type { ReachProperties } from '../api/types'
import { cm, signedCm } from '../lib/derived'
import { formatMeasuredAt, freshnessLabel, trendArrow, trendOf } from '../lib/freshness'
import { BUCKET_ORDER, bucketOf } from '../lib/levels'
import { StatusDot } from './StatusMark'

type Sort = 'severity' | 'name' | 'change'

const SORTS: { key: Sort; label: string }[] = [
  { key: 'severity', label: 'Stanje' },
  { key: 'name', label: 'Ime' },
  { key: 'change', label: 'Promjena' },
]

/**
 * Tabelarna alternativa mapi (UI.md §5).
 *
 * Nije rezervna varijanta nego ravnopravan prikaz: čitač ekrana i tastatura ovdje dobijaju
 * isto što i miš na mapi. Zato je uvijek u DOM-u, i kad je sekcija sklopljena.
 *
 * Podrazumijevani poredak je **po ozbiljnosti**, ne abecedni. Abeceda je bila pogrešan
 * izbor: u poplavi je jedina crvena dionica mogla biti na 61. mjestu od 77, ispod desetina
 * mirnih. Poredak ne mijenja nijedan podatak — bira šta se vidi bez skrolanja.
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
  const [sort, setSort] = useState<Sort>('severity')

  const sorted = useMemo(() => {
    const byName = (a: ReachProperties, b: ReachProperties) =>
      (a.name ?? '').localeCompare(b.name ?? '', 'bs')

    const copy = [...reaches]

    if (sort === 'name') return copy.sort(byName)

    if (sort === 'change') {
      // Najveća apsolutna promjena prvo — nagli pad je jednako vrijedan pažnje kao rast.
      // Dionice bez promjene idu na kraj, ne na vrh sa nulom.
      return copy.sort((a, b) => {
        const av = a.changeCm === null || a.changeCm === undefined ? -1 : Math.abs(a.changeCm)
        const bv = b.changeCm === null || b.changeCm === undefined ? -1 : Math.abs(b.changeCm)
        return bv - av || byName(a, b)
      })
    }

    return copy.sort(
      (a, b) => BUCKET_ORDER.indexOf(bucketOf(a)) - BUCKET_ORDER.indexOf(bucketOf(b)) || byName(a, b),
    )
  }, [reaches, sort])

  return (
    <>
      <div className="mb-2 flex items-center justify-between gap-3">
        <span className="text-xs text-fg-muted">Poredaj po</span>

        <div
          role="group"
          aria-label="Poredak liste"
          className="flex rounded-chip border border-line bg-ink-800 p-0.5"
        >
          {SORTS.map((option) => (
            <button
              key={option.key}
              type="button"
              onClick={() => setSort(option.key)}
              aria-pressed={sort === option.key}
              className={`rounded-chip px-2.5 py-1 text-xs transition-colors ${
                sort === option.key
                  ? 'bg-ink-600 text-fg'
                  : 'text-fg-muted hover:text-fg-soft'
              }`}
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>

      {sorted.length === 0 && (
        <p className="rounded-card border border-line bg-ink-850 px-3 py-3 text-sm text-fg-muted">
          Nijedna dionica ne odgovara odabranom filteru. Traka „Stanje" iznad pokazuje pune
          brojeve — ništa nije nestalo iz podataka.
        </p>
      )}

      <table className="w-full border-collapse text-sm">
        <caption className="sr-only">
          Stanje dionica i mjernih mjesta, sa vremenom mjerenja i izvorom podatka.
        </caption>
        <thead className="sr-only">
          <tr>
            <th scope="col">Dionica</th>
            <th scope="col">Vodostaj</th>
          </tr>
        </thead>
        <tbody>
          {sorted.map((reach) => {
            const key = `${reach.sourceId}-${reach.stationKey}`
            const measured = formatMeasuredAt(reach.measuredAt)
            const selected = key === selectedKey
            const trend = trendOf(reach)
            const hasValue = reach.valueCm !== null && reach.valueCm !== undefined

            return (
              <tr
                key={key}
                className={`border-b border-line/50 last:border-b-0 ${
                  selected ? 'bg-ink-800' : 'hover:bg-ink-850'
                }`}
              >
                <th scope="row" className="p-0 text-left font-normal">
                  <button
                    type="button"
                    onClick={() => onSelect(reach)}
                    className="flex w-full items-center gap-3 py-2.5 pr-1 text-left"
                  >
                    {/* Traka u boji ide uz cijelu visinu reda — vidljivija od tačke pri
                        brzom skrolanju kroz 77 redova, a ne uvodi novu boju. */}
                    <span className="flex shrink-0 items-center self-stretch">
                      <StatusDot reach={reach} size={10} />
                    </span>

                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-medium">{reach.name}</span>
                      <span className="block truncate text-xs text-fg-muted">
                        {/* Boja nikad sama — natpis stanja ide uz nju i u tabeli (UI.md §5). */}
                        {reach.river ? `${reach.river} · ` : ''}
                        {reach.levelLabel}
                      </span>
                    </span>

                    <span className="shrink-0 text-right">
                      {hasValue ? (
                        <>
                          <span className="numeric block leading-tight font-semibold">
                            {cm(reach.valueCm as number)}
                            <span className="font-sans text-[11px] font-normal text-fg-muted">
                              {' '}
                              cm
                            </span>
                          </span>

                          <span className="block text-[11px] leading-tight whitespace-nowrap text-fg-muted">
                            {reach.changeCm !== null && reach.changeCm !== undefined ? (
                              <>
                                <span aria-hidden="true">{trendArrow(trend)}</span>{' '}
                                <span className="tabular">{signedCm(reach.changeCm)}</span>
                              </>
                            ) : (
                              (measured ?? '')
                            )}
                          </span>
                        </>
                      ) : (
                        /* Prazno stanje kaže šta se desilo, ne „ups" (UI.md §7). */
                        <span className="block max-w-[9rem] text-[11px] leading-tight text-fg-muted">
                          {reach.noDataReason ?? 'Nema podatka'}
                        </span>
                      )}
                    </span>
                  </button>
                </th>

                {/* Vrijeme mjerenja mora biti dostupno čitaču ekrana i kad ga red vizuelno
                    sažme u strelicu i promjenu (zlatno pravilo 2). */}
                <td className="sr-only">
                  {measured ? `${measured}, ${freshnessLabel(reach)}` : 'bez vremena mjerenja'}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </>
  )
}
