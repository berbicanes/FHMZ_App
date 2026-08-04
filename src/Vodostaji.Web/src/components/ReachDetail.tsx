import type { ReachProperties } from '../api/types'
import { formatMeasuredAt, freshnessLabel, freshnessOf } from '../lib/freshness'

/**
 * Detalj odabrane dionice.
 *
 * Faza 1 pokriva vodostaj, vrijeme mjerenja, pragove i atribuciju. Graf 7/30 dana i trend
 * dolaze u Fazi 2 — dodavanje strelice trenda sada bi značilo da je izmišljamo iz jednog
 * očitanja, a to je izmišljanje podatka.
 */
export function ReachDetail({
  reach,
  onClose,
}: {
  reach: ReachProperties
  onClose: () => void
}) {
  const freshness = freshnessOf(reach)
  const measured = formatMeasuredAt(reach.measuredAt)

  return (
    <aside
      aria-label={`Detalj dionice ${reach.name}`}
      className="border-t border-[--color-border] bg-[--color-surface-raised] p-4"
    >
      <div className="mb-3 flex items-start justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold">{reach.name}</h2>
          {reach.river && (
            <p className="text-xs text-[--color-text-muted]">Rijeka {reach.river}</p>
          )}
        </div>
        <button
          type="button"
          onClick={onClose}
          className="rounded border border-[--color-border] px-2 py-1 text-xs text-[--color-text-muted] hover:text-[--color-text]"
        >
          Zatvori
        </button>
      </div>

      {/* Vodostaj je heroj ekrana (UI.md §6). Brojevi tabularni. */}
      {reach.valueCm !== null && reach.valueCm !== undefined ? (
        <p className="tabular mb-1 text-4xl leading-none font-semibold">
          {reach.valueCm}
          <span className="ml-1.5 text-base font-normal text-[--color-text-muted]">cm</span>
        </p>
      ) : (
        <p className="mb-1 text-2xl leading-none font-semibold text-[--color-text-muted]">
          Nema podatka
        </p>
      )}

      <p className="mb-3 flex items-center gap-2 text-sm">
        <span
          aria-hidden="true"
          className="status-swatch h-3 w-3 shrink-0 rounded-sm border border-black/40"
          style={{
            backgroundColor: reach.color ?? '#cccccc',
            backgroundImage:
              freshness === 'unknown'
                ? 'repeating-linear-gradient(45deg, #5c6470 0 2px, transparent 2px 5px)'
                : undefined,
          }}
        />
        <span>{reach.levelLabel}</span>
      </p>

      <dl className="space-y-1.5 text-sm">
        {measured ? (
          <div className="flex justify-between gap-4">
            <dt className="text-[--color-text-muted]">Mjereno</dt>
            <dd className="text-right">
              {measured} <span className="text-[--color-text-muted]">({freshnessLabel(reach)})</span>
            </dd>
          </div>
        ) : (
          <div className="flex justify-between gap-4">
            <dt className="text-[--color-text-muted]">Zašto nema podatka</dt>
            <dd className="text-right">{reach.noDataReason ?? 'Izvor nije poslao vrijednost.'}</dd>
          </div>
        )}

        {/* Doslovni tekst agencije. Termin koji korisnik vidi mora biti isti kroz cijeli
            flow (UI.md §7), pa se pokazuje i ono što je izvor stvarno rekao. */}
        <div className="flex justify-between gap-4">
          <dt className="text-[--color-text-muted]">Izvor kaže</dt>
          <dd className="text-right">{reach.statusLabelOriginal}</dd>
        </div>
      </dl>

      {reach.thresholds && reach.thresholds.length > 0 && (
        <section className="mt-4">
          <h3 className="mb-1.5 text-xs font-semibold tracking-wide text-[--color-text-muted] uppercase">
            Pragovi
          </h3>
          <ul className="space-y-1 text-sm">
            {reach.thresholds.map((threshold) => (
              <li key={threshold.label} className="flex justify-between gap-4">
                <span className="text-[--color-text-muted]">{threshold.label}</span>
                <span className="tabular">{threshold.valueCm} cm</span>
              </li>
            ))}
          </ul>
          {/* Pragove definiše agencija, i njeno ime ide uz njih (UI.md §3). */}
          <p className="mt-1.5 text-xs text-[--color-text-muted]">
            Pragove definiše {reach.thresholdsDefinedBy}.
          </p>
        </section>
      )}

      {/* Atribucija po dionici, ne u footeru (LEGAL.md §2.1). */}
      <p className="mt-4 border-t border-[--color-border] pt-3 text-xs text-[--color-text-muted]">
        Izvor:{' '}
        <a
          href={reach.sourceUrl ?? reach.agencyUrl ?? '#'}
          target="_blank"
          rel="noreferrer"
          className="underline"
        >
          {reach.agencyName}
        </a>
      </p>
    </aside>
  )
}
