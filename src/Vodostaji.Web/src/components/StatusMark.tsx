import type { ReachProperties } from '../api/types'
import { freshnessOf } from '../lib/freshness'

/**
 * Oznaka stanja: boja, **oblik** i tekst zajedno.
 *
 * Boja nikad nije jedini nosilac (UI.md §5). Dionica bez podatka nosi šrafuru, tačkasti
 * izvori nose prsten — isti oblici kojima se crtaju na mapi, pa oznaka u tekstu i mrlja na
 * mapi govore istu stvar.
 */
export function StatusDot({
  reach,
  size = 12,
}: {
  reach: ReachProperties
  size?: number
}) {
  const unknown = freshnessOf(reach) === 'unknown'
  const ring = reach.sourceId === 'fhmzbih'

  return (
    <span
      aria-hidden="true"
      className="status-swatch inline-block shrink-0 rounded-full"
      style={{
        width: size,
        height: size,
        backgroundColor: reach.color ?? '#cccccc',
        backgroundImage: unknown
          ? 'repeating-linear-gradient(45deg, #8a97a8 0 2px, transparent 2px 5px)'
          : undefined,
        border: ring ? '2px solid #0b1018' : '1px solid rgb(0 0 0 / 0.45)',
      }}
    />
  )
}

/**
 * Stanje kao čip. Tekst je uvijek prisutan — oznaka koja se oslanja na boju ne radi za
 * daltoniste ni na crno-bijelom ekranu.
 */
export function StatusPill({ reach }: { reach: ReachProperties }) {
  return (
    <span className="inline-flex items-center gap-2 rounded-chip border border-line bg-ink-800 px-2.5 py-1 text-xs font-medium">
      <StatusDot reach={reach} size={10} />
      <span>{reach.levelLabel}</span>
    </span>
  )
}
